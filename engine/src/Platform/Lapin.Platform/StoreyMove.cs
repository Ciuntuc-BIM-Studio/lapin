using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The heights a module's element keeps relative to its storey: each
/// attribute, and whether it counts from the storey's floor (a wall's base
/// offset) or from its top (a beam's top offset, a roof's wall plate).
/// </summary>
public interface IStoreyLevels
{
    IEnumerable<(string Key, bool FromTop)> LevelsOf(Entity e);
}

/// <summary>
/// An element whose heights are not its storey's but absolute, measured down
/// from a datum it keeps (a terrain: its points' heights less its datum).
/// Moved with the storey, the datum changes by the difference, so it rises or
/// falls with the level as the rest do (report #43); kept in place, it stays.
/// </summary>
public interface IStoreyDatum
{
    IEnumerable<string> DatumsOf(Entity e);
}

/// <summary>
/// Moving elements to another storey (the old Lapin's Move to storey): each
/// leaves its storey for the target, with what it hosts (its openings' doors
/// and windows) and the parts it aggregates. Kept with the storey (the
/// default), its heights stay as they were, so it moves with the level: a wall
/// drawn on the ground floor stands on the first floor. Kept in place, its
/// heights change by the difference, so it stays where it was in space. Joins
/// to elements left behind go.
/// </summary>
public static class StoreyMove
{
    public static void Register(IModuleRegistry r)
    {
        r.Command("element.moveToStorey", "Move to storey", (ctx, a) =>
        {
            var target = Spatial.Require(ctx.Model, (string?)a["storeyId"], IfcClass.BuildingStorey);
            var keep = (string?)a["keep"] ?? "storey";
            if (keep is not ("storey" or "world")) throw new CommandException("Keep the elements with the storey (storey) or where they are (world).");
            var ids = Args.Ids(a).Distinct().ToList();
            if (ids.Count == 0) throw new CommandException("Select what to move to another storey.");
            var picked = ids.Select(id => ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.")).ToList();
            var moving = new List<Entity>();
            foreach (var e in picked)
            {
                // A door or window goes with its wall.
                if (Host(ctx.Model, e) is { } host)
                {
                    if (!ids.Contains(host.Id)) throw new CommandException($"„{e.Name}” moves with „{host.Name}”: move that.");
                    continue;
                }
                if (Spatial.StoreyOf(ctx.Model, e.Id) is null) throw new CommandException($"„{e.Name}” is on no storey.");
                moving.Add(e);
            }
            return new JsonObject { ["moved"] = Move(ctx, moving, target, keep) };
        });

        // A copy of a storey, right above it, with its height; the storeys above move up by that height (their
        // elements follow: heights are relative). With contents, every element on it is copied (with what it hosts)
        // and put on the new storey, and the joins between the copies are made again from the originals'.
        r.Command("storey.duplicate", "Duplicate storey", (ctx, a) =>
        {
            var src = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.BuildingStorey);
            var withContents = !(a["withContents"] is JsonValue wv && wv.TryGetValue<bool>(out var w) && !w);
            var h = src.Num("height", 3);
            var elev = src.Num("elevation");
            foreach (var s in Spatial.Storeys(ctx.Model).Where(s => s.Id != src.Id && s.Num("elevation") > elev + 1e-9).ToList())
            {
                var sa = s.CloneAttrs();
                sa["elevation"] = s.Num("elevation") + h;
                ctx.Replace(s.With(attrs: sa));
            }
            var attrs = src.CloneAttrs();
            attrs["elevation"] = elev + h;
            attrs["height"] = h;
            var storey = new Entity(ctx.NewId(), IfcClass.BuildingStorey, (string?)a["name"] is { Length: > 0 } nm ? nm : Views.CopyName(ctx.Model, src.Name, IfcClass.BuildingStorey), attrs);
            ctx.Add(storey);
            ctx.Relate(IfcClass.RelAggregates, Spatial.EnsureBuilding(ctx).Id, storey.Id);
            var copied = new JsonArray();
            if (withContents)
            {
                var map = new Dictionary<string, string>();
                // What a wall hosts goes with its copy; a reference from an IFC file stays where the file put it.
                var originals = ctx.Model.RelatedOf(src.Id, IfcClass.RelContainedInSpatialStructure)
                    .Where(e => Host(ctx.Model, e) == null && !StoredBodies.Has(e) && ctx.Model.RelatingOf(e.Id, IfcClass.RelVoidsElement) == null).ToList();
                foreach (var e in originals)
                {
                    var id = Placement.Copy(ctx, e, Placement2D.Move(default));
                    map[e.Id] = id;
                    copied.Add((JsonNode)id);
                }
                Move(ctx, map.Values.Select(id => ctx.Model.Get(id)).OfType<Entity>().ToList(), storey, "storey");
                foreach (var rel in ctx.Model.Relations.Where(x => x.Class == Joins.Class && map.ContainsKey(x.Relating) && x.Related.All(map.ContainsKey)).ToList())
                    ctx.AddRelation(new Relation(ctx.NewId(), rel.Class, map[rel.Relating], rel.Related.Select(i => map[i]).ToList(), rel.Attrs?.DeepClone() as JsonObject));
            }
            return new JsonObject { ["id"] = storey.Id, ["copied"] = copied };
        });
    }

    /// <summary>The elements (and what they host and aggregate) to <paramref name="target"/>, kept with the storey or where they are; the ids moved.</summary>
    public static JsonArray Move(ICommandContext ctx, IReadOnlyList<Entity> moving, Entity target, string keep)
    {
        {
            // With what it hosts and aggregates.
            var all = new List<Entity>();
            var seen = new HashSet<string>();
            void Take(Entity e)
            {
                if (!seen.Add(e.Id)) return;
                all.Add(e);
                foreach (var o in ctx.Model.RelatedOf(e.Id, IfcClass.RelVoidsElement))
                    foreach (var fill in ctx.Model.RelatedOf(o.Id, IfcClass.RelFillsElement)) Take(fill);
                foreach (var part in ctx.Model.RelatedOf(e.Id, IfcClass.RelAggregates)) Take(part);
            }
            foreach (var e in moving) Take(e);
            var levels = ctx.Services.All<IStoreyLevels>();
            var moved = new JsonArray();
            foreach (var e in all)
            {
                if (Spatial.StoreyOf(ctx.Model, e.Id) is not { } from || from.Id == target.Id) continue;
                if (keep == "world")
                {
                    var attrs = e.CloneAttrs();
                    var changed = false;
                    foreach (var (key, fromTop) in levels.SelectMany(l => l.LevelsOf(e)).Distinct())
                    {
                        double Ref(Entity s) => s.Num("elevation") + (fromTop ? s.Num("height", 3) : 0);
                        attrs[key] = e.Num(key) + Ref(from) - Ref(target);
                        changed = true;
                    }
                    if (changed) ctx.Replace(e.With(attrs: attrs));
                }
                else if (ctx.Services.All<IStoreyDatum>().SelectMany(d => d.DatumsOf(e)).Distinct().ToList() is { Count: > 0 } datums)
                {
                    var attrs = e.CloneAttrs();
                    foreach (var key in datums) attrs[key] = e.Num(key) + from.Num("elevation") - target.Num("elevation");
                    ctx.Replace(e.With(attrs: attrs));
                }
                Spatial.Contain(ctx, target.Id, e.Id);
                moved.Add((JsonNode)e.Id);
            }
            // Joins only hold on one storey.
            var set = all.Select(e => e.Id).ToHashSet();
            foreach (var rel in ctx.Model.Relations.Where(x => x.Class == Joins.Class && (set.Contains(x.Relating) != x.Related.Any(set.Contains))).ToList())
                ctx.RemoveRelation(rel.Id);
            return moved;
        }
    }

    /// <summary>The wall a door or window fills an opening of (null: it stands on its own).</summary>
    static Entity? Host(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening ? m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) : null;
}
