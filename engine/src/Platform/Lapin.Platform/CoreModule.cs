using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The platform's own module, in every product: the spatial structure
/// (IfcProject, IfcSite, IfcBuilding, IfcBuildingStorey), the materials and
/// the commands every element answers to (rename, delete).
/// </summary>
public sealed class CoreModule : ILapinModule
{
    public const string ModuleId = "core";
    public string Id => ModuleId;
    public string Name => "Core";

    static readonly HashSet<string> PlatformClasses =
    [
        IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey,
        IfcClass.Material, IfcClass.MaterialLayerSet, IfcClass.MaterialLayerSetUsage, Views.Class, Sheets.Class,
    ];

    public static bool Owns(string ifcClass) => PlatformClasses.Contains(ifcClass) || ifcClass.StartsWith("IfcRel", StringComparison.Ordinal);

    public void Register(IModuleRegistry r)
    {
        // Booleans for whatever cannot be built constructively (openings through walls); none: callers degrade with a diagnostic.
        if (BubbleBim.Platform.Booleans.ManifoldBooleans.Available) r.Service<BubbleBim.Geometry.ISolidBooleans>(new BubbleBim.Platform.Booleans.ManifoldBooleans());

        r.Class(new(IfcClass.BuildingStorey, "Storey",
            [new("elevation", "Elevation", "length"), new("height", "Height", "length", Min: 0.1)], "#6c5ce7", Kind: "spatial"));
        r.Class(new(IfcClass.Material, "Material", [new("color", "Colour", "color"), new("category", "Category", "text")], Kind: "resource"));
        DraftingResources.Register(r);
        Views.Register(r);
        GraphicsSets.Register(r);
        ViewLinework.Register(r);
        Location.Register(r);
        Sheets.Register(r);

        r.Command("storey.create", "New storey", (ctx, a) =>
        {
            var building = Spatial.EnsureBuilding(ctx);
            var storeys = Spatial.Storeys(ctx.Model).ToList();
            var top = storeys.LastOrDefault();
            var elevation = Json.Number(a["elevation"]) ?? (top == null ? 0 : top.Num("elevation") + top.Num("height", 3));
            var name = (string?)a["name"] ?? (storeys.Count == 0 ? "Ground floor" : $"Level {storeys.Count}");
            var storey = new Entity(ctx.NewId(), IfcClass.BuildingStorey, name, new JsonObject
            {
                ["elevation"] = elevation,
                ["height"] = Json.Number(a["height"]) ?? 3.0,
            });
            ctx.Add(storey);
            ctx.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
            return new JsonObject { ["id"] = storey.Id };
        });

        r.Command("storey.update", "Change storey", (ctx, a) =>
        {
            var s = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.BuildingStorey);
            var attrs = s.CloneAttrs();
            if (Json.Number(a["elevation"]) is { } e) attrs["elevation"] = e;
            if (Json.Number(a["height"]) is { } h)
            {
                if (!(h > 0)) throw new CommandException("A storey's height must be positive.");
                attrs["height"] = h;
            }
            ctx.Replace(s.With(name: (string?)a["name"], attrs: attrs));
            return null;
        });

        r.Command("element.rename", "Rename", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Nothing to rename.");
            var name = ((string?)a["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("A name cannot be empty.");
            ctx.Replace(e.With(name: name));
            return null;
        });

        r.Command("element.update", "Change", (ctx, a) =>
        {
            // One element, or several (ids): each takes the same values.
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Nothing to change.");
            var values = a["attrs"] as JsonObject ?? throw new CommandException("Nothing to change.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Nothing to change.");
                if (StoredBodies.Has(e)) throw new CommandException($"„{e.Name}” is a reference from an IFC file: it can be deleted, not changed.");
                var d = ctx.Describe(e.Class) ?? throw new CommandException($"{e.Class} cannot be changed here.");
                var attrs = e.CloneAttrs();
                foreach (var (key, value) in values)
                {
                    var f = d.Fields.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"{d.Label} has no field „{key}”.");
                    if (f.ReadOnly) throw new CommandException($"{f.Label} cannot be changed.");
                    if (value is null) { attrs.Remove(key); continue; }
                    attrs[key] = Fields.Check(f, value);
                }
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Move, copy, rotate or mirror elements and annotations in plan (one undo step); a copy returns the new ids.
        r.Command("element.transform", "Transform", (ctx, a) =>
        {
            static BubbleBim.Geometry.Vec2 V(JsonNode? n, string what) => Args.Point(n, what);
            var ids = Args.Ids(a).ToList();
            var op = (string?)a["op"] ?? "move";
            var t = op switch
            {
                "move" or "copy" => Placement2D.Move(V(a["by"], "displacement")),
                "rotate" => Placement2D.Rotate(V(a["center"], "centre"), (Json.Number(a["angle"]) ?? 0) * Math.PI / 180),
                "mirror" => (V(a["a"], "mirror line's start") - V(a["b"], "mirror line's end")).Length < 1e-9
                    ? throw new CommandException("A mirror line needs two different points.") : Placement2D.Mirror(V(a["a"], "start"), V(a["b"], "end")),
                _ => throw new CommandException($"„{op}” is not move, copy, rotate or mirror."),
            };
            var copy = op == "copy" || (a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb);
            var entities = ids.Select(id => ctx.Model.Get(id)).OfType<Entity>().ToList();
            // What a module places itself (an opening in its wall) goes with its host, not on its own.
            entities = entities.Where(e => e.Class is not (IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey)
                && ctx.Model.RelatingOf(e.Id, IfcClass.RelFillsElement) == null && ctx.Model.RelatingOf(e.Id, IfcClass.RelVoidsElement) == null).ToList();
            if (entities.FirstOrDefault(StoredBodies.Has) is { } fixedOne) throw new CommandException($"„{fixedOne.Name}” is a reference from an IFC file: it stays where the file put it.");
            var moving = entities.Select(e => e.Id).ToHashSet();
            var made = new JsonArray();
            foreach (var e in entities)
            {
                if (ctx.Model.Get(e.Id) is not { } now) continue;
                if (copy) made.Add(Placement.Copy(ctx, now, t));
                else Placement.Transform(ctx, now, t, moving);
            }
            return new JsonObject { ["ids"] = made };
        });

        r.Command("element.delete", "Delete", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if ((string?)a["id"] is { } one) ids.Add(one);
            foreach (var id in ids.Distinct())
            {
                if (ctx.Model.Get(id) is not { } e)
                {
                    foreach (var d in ctx.Services.All<IDeletion>()) if (d.Delete(ctx, id)) break;
                    continue;
                }
                if (e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building) throw new CommandException($"The {e.Class} cannot be deleted.");
                if (e.Class == IfcClass.BuildingStorey && ctx.Model.RelatedOf(e.Id, IfcClass.RelContainedInSpatialStructure).Any())
                    throw new CommandException($"„{e.Name}” still has elements on it.");
                // A type in use stays: the elements built with it would be left without one.
                if (ctx.Describe(e.Class)?.Kind == "type" && ctx.Model.RelatedOf(e.Id, IfcClass.RelDefinesByType).Where(x => !ids.Contains(x.Id)).Count() is var n and > 0)
                    throw new CommandException($"„{e.Name}” is used by {n} element(s): give them another type first.");
                ctx.Remove(id);
            }
            return null;
        });
    }
}

/// <summary>The spatial structure: one project, site and building; the storeys, lowest first.</summary>
public static class Spatial
{
    public static Entity? Project(IModelView m) => m.OfClass(IfcClass.Project).FirstOrDefault();
    public static Entity? Building(IModelView m) => m.OfClass(IfcClass.Building).FirstOrDefault();

    public static IEnumerable<Entity> Storeys(IModelView m) =>
        m.OfClass(IfcClass.BuildingStorey).OrderBy(s => s.Num("elevation")).ThenBy(s => m.Order(s.Id));

    /// <summary>The storey an element stands on.</summary>
    public static Entity? StoreyOf(IModelView m, string elementId) => m.RelatingOf(elementId, IfcClass.RelContainedInSpatialStructure);

    /// <summary>The spatial parent: an element's storey, a storey's building, a building's site.</summary>
    public static Entity? ParentOf(IModelView m, string id) =>
        StoreyOf(m, id) ?? m.RelatingOf(id, IfcClass.RelAggregates);

    public static Entity Require(IModelView m, string? id, string ifcClass) =>
        id != null && m.Get(id) is { } e && e.Class == ifcClass ? e : throw new CommandException($"There is no {ifcClass} {id}.");

    /// <summary>The project, site and building, made when the model has none yet.</summary>
    public static Entity EnsureBuilding(IEdit edit)
    {
        if (Building(edit.Model) is { } b) return b;
        var project = Project(edit.Model);
        if (project == null)
        {
            project = new Entity(edit.NewId(), IfcClass.Project, "Project", new JsonObject { ["lengthUnit"] = "METRE" });
            edit.Add(project);
        }
        var site = edit.Model.OfClass(IfcClass.Site).FirstOrDefault();
        if (site == null)
        {
            site = new Entity(edit.NewId(), IfcClass.Site, "Site", []);
            edit.Add(site);
            edit.Relate(IfcClass.RelAggregates, project.Id, site.Id);
        }
        var building = new Entity(edit.NewId(), IfcClass.Building, "Building", []);
        edit.Add(building);
        edit.Relate(IfcClass.RelAggregates, site.Id, building.Id);
        return building;
    }

    /// <summary>Places <paramref name="elementId"/> on <paramref name="storeyId"/>.</summary>
    public static void Contain(IEdit edit, string storeyId, string elementId)
    {
        Require(edit.Model, storeyId, IfcClass.BuildingStorey);
        edit.Relate(IfcClass.RelContainedInSpatialStructure, storeyId, elementId);
    }
}

/// <summary>The project's materials (IfcMaterial), found by name.</summary>
public static class Materials
{
    public static Entity? Find(IModelView m, string name) =>
        m.OfClass(IfcClass.Material).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The material named <paramref name="name"/>, made when the project has none.</summary>
    public static Entity Ensure(IEdit edit, string name, string? color = null)
    {
        if (Find(edit.Model, name) is { } found) return found;
        var m = new Entity(edit.NewId(), IfcClass.Material, name, new JsonObject { ["color"] = color ?? DefaultColor(name) });
        edit.Add(m);
        return m;
    }

    public static string ColorOf(IModelView m, string? materialId) =>
        materialId != null && m.Get(materialId) is { } mat ? mat.Str("color") ?? DefaultColor(mat.Name) : "#c9ced6";

    /// <summary>A colour from the material's name, until the project gives it one.</summary>
    public static string DefaultColor(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("brick") || n.Contains("cărămid")) return "#b5654a";
        if (n.Contains("concrete") || n.Contains("beton")) return "#a8a8a8";
        if (n.Contains("insulation") || n.Contains("xps") || n.Contains("eps") || n.Contains("wool")) return "#f2c46d";
        if (n.Contains("plaster") || n.Contains("render") || n.Contains("tencu")) return "#e8e2d6";
        if (n.Contains("timber") || n.Contains("wood") || n.Contains("lemn")) return "#c08a4f";
        if (n.Contains("aac") || n.Contains("bca")) return "#d9d6cf";
        return "#c9ced6";
    }
}

/// <summary>A value checked against the field that holds it.</summary>
public static class Fields
{
    public static JsonNode Check(FieldDescriptor f, JsonNode value)
    {
        switch (f.Kind)
        {
            case "number" or "length" or "angle":
            {
                var n = Json.Number(value) ?? throw new CommandException($"{f.Label} must be a number.");
                if (!double.IsFinite(n)) throw new CommandException($"{f.Label} must be a number.");
                if (f.Min is { } min && n < min) throw new CommandException($"{f.Label} must be at least {min}.");
                if (f.Max is { } max && n > max) throw new CommandException($"{f.Label} must be at most {max}.");
                return n;
            }
            case "bool":
                return value is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f.Label} is yes or no.");
            case "choice":
            {
                var c = (string?)value;
                return c != null && (f.Choices ?? []).Contains(c) ? c : throw new CommandException($"{f.Label} must be one of {string.Join(", ", f.Choices ?? [])}.");
            }
            case "color":
            {
                var c = (string?)value;
                return c is { Length: 7 } && c[0] == '#' && c[1..].All(Uri.IsHexDigit) ? c : throw new CommandException($"{f.Label} is a colour like #a8a8a8.");
            }
            default:
                return (string?)value ?? throw new CommandException($"{f.Label} must be text.");
        }
    }
}
