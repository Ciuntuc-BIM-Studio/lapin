using Lapin.Model;

namespace Lapin.Platform;

/// <summary>One part of a cost item's unit rate as IFC writes it (an IfcCostValue): its category (Material, Labour, Equipment, Transport) and the value per unit of the item's quantity.</summary>
public sealed record IfcCostValueData(string Category, double UnitValue);

/// <summary>
/// An IfcCostItem: its GlobalId, name, code (Identification), the item it is nested in (IfcRelNests; null at the top: a
/// chapter, or an item of no chapter), its quantity (a value and its kind: VOLUME, AREA, LENGTH, COUNT, WEIGHT; none for a
/// chapter), its unit rates by category, the elements it measures (IfcRelAssignsToControl, by id; GlobalIds in a file) and
/// the module's own fields as a property set (the unit as the price list writes it); the tasks its work is done in (stage 7:
/// IfcRelAssignsToControl to IfcTasks, by id; GlobalIds in a file). Siblings come in their order.
/// </summary>
public sealed record IfcCostItemData(string Id, string Name, string? Identification, string? Parent, double? Quantity, string? QuantityKind,
    IReadOnlyList<IfcCostValueData> Values, IReadOnlyList<string> Elements, string PropertySet, IReadOnlyList<(string Name, string Value)> Properties,
    IReadOnlyList<string>? Tasks = null);

/// <summary>
/// A cost schedule as IFC has it (docs/plan-4d5d.md, stage 6): the IfcCostSchedule (its GlobalId, name, PredefinedType
/// COSTPLAN or PRICEDBILLOFQUANTITIES, its currency, the project's IfcMonetaryUnit), declared by the project, controlling
/// its top items (IfcRelAssignsToControl); the items nested in one another; the schedule's own fields (the overheads,
/// profit and VAT rates) as a property set.
/// </summary>
public sealed record IfcCostScheduleData(string Id, string Name, string Predefined, string Currency, IReadOnlyList<IfcCostItemData> Items,
    string PropertySet, IReadOnlyList<(string Name, string Value)> Properties);

/// <summary>What reading a file's cost schedules did, said to the user.</summary>
public sealed record IfcControlImport(int Schedules, int Items, IReadOnlyList<string> Notes);

/// <summary>
/// A module's controls in IFC (the cost module's estimate): what the export writes, and what the import hands back read
/// from a file, for the module to make its own entities of. Offered as a service; the export and the import work without one.
/// </summary>
public interface IIfcControls
{
    /// <summary>The cost schedules as the export writes them, read through a query.</summary>
    IEnumerable<IfcCostScheduleData> CostSchedules(IQueryContext query);
    /// <summary>The file's cost schedules made the module's own; <paramref name="idOf"/> gives the project's id of an element the file names by GlobalId, null when the project has none.</summary>
    IfcControlImport Import(IEdit edit, IReadOnlyList<IfcCostScheduleData> schedules, Func<string, string?> idOf);
}
