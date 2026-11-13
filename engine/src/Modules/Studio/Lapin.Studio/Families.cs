using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// The light families of decision 515 (docs/plan-user-extensibility.md §5.6), made parts by decision 517 (docs/plan-studio-parametric.md
/// §9): a family is a part (<see cref="Parts"/>: a <c>LapinSolidType</c> carrying a definition), an object of a family a placed part.
/// The family commands and queries stay, with the same names, arguments and results, as shortcuts onto the part ones; the
/// <c>.lapinfamily</c> file is still written by <c>studio.exportFamily</c> and read by both imports. Projects saved with families are
/// upgraded when they open (<see cref="FamilyPartUpgrade"/>).
/// </summary>
public static class Families
{
    /// <summary>The class a family is now: a part type.</summary>
    public const string Class = Parts.TypeClass;
    /// <summary>The class an object of a family is now: a placed part (a Studio solid).</summary>
    public const string InstanceClass = Parts.InstanceClass;
    public const string FileFormat = "lapin-family";
    public static readonly string[] Planes = Parts.Planes;
    public static readonly string[] ParamKinds = Parts.ParamKinds;

    public static void Register(IModuleRegistry r)
    {
        // The project's families: its parts that have a definition.
        r.Query("families", (q, _) => new JsonObject
        {
            ["families"] = new JsonArray(q.Model.OfClass(Class).Where(e => Parts.DefOf(e) != null).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["def"] = e.Attrs["def"]?.DeepClone(), ["uses"] = Parts.Uses(q.Model, e),
            }).ToArray()),
        });
        r.Query("familyPreview", (q, a) => Parts.Preview(a, q.Model));
        r.Command("createFamily", "New family", (ctx, a) => Parts.Create(ctx, a, "Family"));
        r.Command("updateFamily", "Change family", (ctx, a) => Parts.Update(ctx, a, Parts.Require(ctx.Model, (string?)a["id"])));
        r.Command("deleteFamily", "Delete family", (ctx, a) => Parts.Delete(ctx, Parts.Require(ctx.Model, (string?)a["id"])));
        // An object of a family: {familyId, storeyId, at: [x, y], z?, rotation?, mirror?, values?}.
        r.Command("placeFamily", "Family object", (ctx, a) => Parts.Place(ctx, a, Parts.Require(ctx.Model, (string?)a["familyId"])));
        r.Command("setFamilyValues", "Family values", Parts.SetValues);
        r.Command("familyFromSketch", "Family from sketch", (ctx, a) => Parts.FromSketch(ctx, a, "Family"));
        r.Query("exportFamily", (q, a) => Parts.FileText(Parts.Require(q.Model, (string?)a["id"]), FileFormat, 1, ".lapinfamily"));
        r.Command("importFamily", "Import family", Parts.Import);
    }
}
