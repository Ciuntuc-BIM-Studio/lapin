using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A work calendar as IFC writes it (IfcWorkCalendar): the weekdays worked (ISO, 1 Monday … 7 Sunday) with the working
/// hours of a day (one IfcWorkTime on a WEEKLY IfcRecurrencePattern), the days not worked (an IfcWorkTime each among the
/// exception times, From to To inclusive), and the module's own rules as a property set.
/// </summary>
public sealed record IfcCalendarData(string Id, string Name, IReadOnlyList<int> Weekdays, TimeOnly DayStart, TimeOnly DayEnd,
    IReadOnlyList<(DateOnly From, DateOnly To, string Name)> Exceptions, string PropertySet, IReadOnlyList<(string Name, string Value)> Properties);

/// <summary>
/// An IfcTaskTime: the scheduled duration in working days, the scheduled (early) start and finish, the late ones, the
/// floats in working days and whether the task is critical; the times of day are the calendar's. What a file leaves out is null.
/// </summary>
public sealed record IfcTaskTimeData(double? DurationDays, DateTime? ScheduleStart, DateTime? ScheduleFinish, DateTime? LateStart = null, DateTime? LateFinish = null,
    double? FreeFloatDays = null, double? TotalFloatDays = null, bool? IsCritical = null, double? Completion = null);

/// <summary>
/// An IfcTask of a schedule: its GlobalId, name, WBS code (Identification), the task it is nested in (IfcRelNests; null at
/// the top), whether it is a milestone, its IfcTaskTypeEnum (null: NOTDEFINED), its time, and the module's own fields as a
/// property set (a constraint, whether it is wet). Siblings come in their order.
/// </summary>
public sealed record IfcTaskData(string Id, string Name, string? Description, string? Identification, string? Parent, bool IsMilestone, string? Predefined,
    IfcTaskTimeData? Time, string PropertySet, IReadOnlyList<(string Name, string Value)> Properties);

/// <summary>An IfcRelSequence: its GlobalId, the predecessor and successor tasks, FINISH_START / START_START / FINISH_FINISH / START_FINISH, the lag in working days.</summary>
public sealed record IfcSequenceData(string Id, string Relating, string Related, string Type, double LagDays);

/// <summary>
/// A work schedule as IFC has it (docs/plan-4d5d.md): the IfcWorkSchedule (its GlobalId, name, start, PredefinedType),
/// declared by the project, controlling its top tasks (IfcRelAssignsToControl); its calendar controlling the schedule and
/// its tasks; the tasks nested in one another; the sequences between them.
/// </summary>
public sealed record IfcScheduleData(string Id, string Name, DateTime Start, DateTime? Finish, string Predefined, IfcCalendarData? Calendar,
    IReadOnlyList<IfcTaskData> Tasks, IReadOnlyList<IfcSequenceData> Sequences);

/// <summary>What reading a file's schedules did, said to the user.</summary>
public sealed record IfcProcessImport(int Schedules, int Tasks, int Sequences, IReadOnlyList<string> Notes);

/// <summary>
/// A module's processes in IFC (the planning module's work schedule): what the export writes, and what the import hands
/// back read from a file, for the module to make its own entities of. Offered as a service; the export and the import work without one.
/// </summary>
public interface IIfcProcesses
{
    IEnumerable<IfcScheduleData> Schedules(IModelView model);
    IfcProcessImport Import(IEdit edit, IReadOnlyList<IfcScheduleData> schedules);
}
