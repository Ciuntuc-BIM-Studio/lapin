using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>Columns, beams and footings given another type of their kind (Manage → Map imported types): a strip footing lays the type's layers, the rest take its section and sizes.</summary>
public sealed class TypeAssigner : ITypeAssigner
{
    public IReadOnlyCollection<string> TypeClasses { get; } = [IfcClass.ColumnType, IfcClass.BeamType, IfcClass.FootingType];

    public void Assign(IEdit edit, IServices services, Entity element, Entity type)
    {
        if (element.Class == IfcClass.Footing && element.PredefinedType == "STRIP_FOOTING") StripFootings.SetType(edit, element, type);
        else edit.Relate(IfcClass.RelDefinesByType, type.Id, element.Id);
    }
}
