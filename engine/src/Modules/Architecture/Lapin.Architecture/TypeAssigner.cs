using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Walls, floors, doors and windows given another type of their kind (Manage → Map imported types): a wall keeps its
/// core's middle where the old type had it (an imported wall's axis is the file's reference line, often a face), a
/// floor and a door or window as their own commands give a type.
/// </summary>
public sealed class TypeAssigner : ITypeAssigner
{
    public IReadOnlyCollection<string> TypeClasses { get; } = [IfcClass.WallType, IfcClass.SlabType, IfcClass.DoorType, IfcClass.WindowType];

    public void Assign(IEdit edit, IServices services, Entity element, Entity type)
    {
        switch (element.Class)
        {
            case IfcClass.Wall:
                var bands = Walls.Section(edit.Model, element);
                var core = bands.Any(b => b.Core) ? bands.First(b => b.Core) : bands.MaxBy(b => b.To - b.From);
                var middle = (core.From + core.To) / 2;
                edit.Relate(IfcClass.RelDefinesByType, type.Id, element.Id);
                LayerSets.LayOut(edit, edit.Model.Get(element.Id)!, type, "AXIS2", (layers, negative) =>
                {
                    var i = Layers.CoreIndex(layers);
                    var before = layers.Take(i).Sum(l => l.Thickness);
                    return Math.Round(middle - (negative ? -1 : 1) * (before + layers[i].Thickness / 2), 6);
                });
                break;
            case IfcClass.Slab:
                Slabs.SetType(edit, element, type);
                break;
            case IfcClass.Door or IfcClass.Window:
                Openings.SetType(edit, element, type);
                break;
            default:
                edit.Relate(IfcClass.RelDefinesByType, type.Id, element.Id);
                break;
        }
    }
}
