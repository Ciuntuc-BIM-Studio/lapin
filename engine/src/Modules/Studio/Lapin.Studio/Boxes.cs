using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A box's bottom and top (docs/plan-studio-thick.md): each a separate Studio solid written as an IfcSlab
/// (<c>operation: boxSlab</c>, <c>box</c>, <c>side</c>), made when the box's <c>bottom</c> or <c>top</c> is switched on and
/// taken away when it is switched off, its thickness the box's <c>bottomThickness</c> or <c>topThickness</c>. A box with
/// a bottom or a top is grouped with them in an IfcElementAssembly (IfcRelAggregates), which goes when neither is left.
/// </summary>
/// <remarks>Also refuses a ring's or a box's justification on a wall strip, and the other way round, when one is changed in the properties panel.</remarks>
public sealed class Boxes : IElementUpdate, IDeletion
{
    public const string AssemblyClass = "IfcElementAssembly";
    static readonly string[] Sides = ["bottom", "top"];

    static bool On(Entity box, string side) => box.Attrs[side] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static IEnumerable<Entity> SlabsOf(IModelView m, string boxId) =>
        m.OfClass(IfcClass.BuildingElementProxy).Where(e => e.Str("operation") == Thick.BoxSlab && e.Str("box") == boxId);

    public static Entity? AssemblyOf(IModelView m, string id) => m.RelatingOf(id, IfcClass.RelAggregates) is { Class: AssemblyClass } a ? a : null;

    /// <summary>A bottom and a top fit the box's height together.</summary>
    public static void CheckSlabs(double height, JsonObject box)
    {
        double T(string side) => box[side] is JsonValue v && v.TryGetValue<bool>(out var b) && b ? Json.Number(box[side + "Thickness"]) ?? Json.Number(box["thickness"]) ?? 0.2 : 0;
        if (T("bottom") + T("top") >= Math.Abs(height) - 1e-9) throw new CommandException($"The bottom and the top are thicker together than the box is high ({Math.Abs(height):0.###} m).");
    }

    /// <summary>The box's bottom and top made or taken away as it says, and its assembly with them.</summary>
    public static void Reconcile(ICommandContext ctx, Entity box)
    {
        var storey = Spatial.StoreyOf(ctx.Model, box.Id)?.Id;
        foreach (var side in Sides)
        {
            var have = SlabsOf(ctx.Model, box.Id).FirstOrDefault(s => (s.Str("side") ?? "bottom") == side);
            if (On(box, side) && have == null)
            {
                var slab = new Entity(ctx.NewId(), IfcClass.BuildingElementProxy, $"{box.Name} {side}",
                    new JsonObject { ["operation"] = Thick.BoxSlab, ["box"] = box.Id, ["side"] = side, ["exportAs"] = IfcClass.Slab });
                ctx.Add(slab);
                if (storey != null) Spatial.Contain(ctx, storey, slab.Id);
            }
            else if (!On(box, side) && have != null) ctx.Remove(have.Id);
        }
        var slabs = SlabsOf(ctx.Model, box.Id).ToList();
        var asm = AssemblyOf(ctx.Model, box.Id);
        if (slabs.Count == 0)
        {
            if (asm != null) ctx.Remove(asm.Id);
            return;
        }
        if (asm == null)
        {
            asm = new Entity(ctx.NewId(), AssemblyClass, box.Name, new JsonObject { ["objectType"] = "Box" }, "USERDEFINED");
            ctx.Add(asm);
            if (storey != null) Spatial.Contain(ctx, storey, asm.Id);
        }
        foreach (var id in slabs.Select(s => s.Id).Prepend(box.Id))
            if (AssemblyOf(ctx.Model, id)?.Id != asm.Id) ctx.Relate(IfcClass.RelAggregates, asm.Id, id);
    }

    /// <summary>A box changed in the properties panel: its bottom and top follow.</summary>
    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        // A ring or a box lies inside, outside or centred; a wall strip left, right or centred.
        if (Thick.Is(after.Str("operation")) && after.Str("justify") is { } j && Thick.JustifyProblem(after.Str("operation")!, j) is { } problem) throw new CommandException(problem);
        if (after.Str("operation") != Thick.Box) return;
        if (On(after, "bottom") || On(after, "top")) CheckSlabs(after.NumOrNull("height") ?? 3, after.Attrs);
        if (On(before, "bottom") != On(after, "bottom") || On(before, "top") != On(after, "top")) Reconcile(ctx, after);
    }

    /// <summary>A box goes with its bottom, top and assembly; a bottom or top deleted is switched off on its box.</summary>
    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { } e) return false;
        switch (e.Str("operation"))
        {
            case Thick.Box:
            {
                var asm = AssemblyOf(ctx.Model, id);
                foreach (var s in SlabsOf(ctx.Model, id).ToList()) ctx.Remove(s.Id);
                if (asm != null) ctx.Remove(asm.Id);
                ctx.Remove(id);
                return true;
            }
            case Thick.BoxSlab:
            {
                ctx.Remove(id);
                if (ctx.Model.Get(e.Str("box") ?? "") is { } box)
                {
                    var attrs = box.CloneAttrs();
                    attrs[e.Str("side") ?? "bottom"] = false;
                    ctx.Replace(box.With(attrs: attrs));
                    Reconcile(ctx, ctx.Model.Get(box.Id)!);
                }
                return true;
            }
        }
        return false;
    }
}
