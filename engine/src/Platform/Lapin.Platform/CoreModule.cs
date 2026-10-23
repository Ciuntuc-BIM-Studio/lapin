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
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Nothing to change.");
            var d = ctx.Describe(e.Class) ?? throw new CommandException($"{e.Class} cannot be changed here.");
            var values = a["attrs"] as JsonObject ?? throw new CommandException("Nothing to change.");
            var attrs = e.CloneAttrs();
            foreach (var (key, value) in values)
            {
                var f = d.Fields.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"{d.Label} has no field „{key}”.");
                if (f.ReadOnly) throw new CommandException($"{f.Label} cannot be changed.");
                if (value is null) { attrs.Remove(key); continue; }
                attrs[key] = Fields.Check(f, value);
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Command("element.delete", "Delete", (ctx, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(n => (string?)n).OfType<string>().ToList() : [];
            if ((string?)a["id"] is { } one) ids.Add(one);
            foreach (var id in ids.Distinct())
            {
                if (ctx.Model.Get(id) is not { } e) continue;
                if (e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building) throw new CommandException($"The {e.Class} cannot be deleted.");
                if (e.Class == IfcClass.BuildingStorey && ctx.Model.RelatedOf(e.Id, IfcClass.RelContainedInSpatialStructure).Any())
                    throw new CommandException($"„{e.Name}” still has elements on it.");
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
