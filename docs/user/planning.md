---
title: Planning
order: 97
summary: The work schedule in a Gantt chart: tasks in a WBS, milestones, links with lags, the critical path worked out on a calendar with Romania's legal holidays; to and from MS Project, IFC and Excel.
---

## The Planning tab

![The Planning tab](shots/ribbon-planning.webp)

- **Gantt**: opens the schedule under the view. The screen splits into two rows, so you see the model above and the schedule below.
- **Calendar…**: the working days and hours, the holidays and the winter break (below).
- **Task** and **Milestone**: a new task or milestone before the task selected in the Gantt (or at the end).
- **Indent**, **Outdent**, **Link** and **Unlink**: the WBS and the links of the tasks selected.
- **Import MS Project…**, **Export MS Project…**, **Export XLSX** and **Export IFC…**: the schedule to and from other programs (below).

A project has one schedule. It is made the first time you add a task and starts the next Monday. Change its start in the Gantt's toolbar.

## The Gantt

![The Gantt under the 3D view, a task selected](shots/planning-gantt.webp)

On the left is the task table: the row number, the **WBS** code, the name, the **Duration** in working days, the **Start** and **Finish**, the **Predecessors** and the total **Float**. On the right, each task is a bar on the time scale:

- a **task** is a blue bar, red when it is on the critical path;
- a **summary** (a task with tasks under it) is a dark bracket from its first task's start to its last task's finish;
- a **milestone** is a diamond.

Days not worked are shaded, and today is a dashed line. **Days**, **Weeks**, **Months** and **Quarters** change the scale, as does <kbd>Ctrl</kbd> + the mouse wheel. The four-corners button shows the whole schedule and **Today** scrolls to today. **Critical path** turns the red on and off. The arrow next to a summary's name folds and unfolds the tasks under it.

The Gantt is a view like the others: it has a tab, and on the desktop it can open in a window of its own for a second screen.

### Editing in the table

Double-click a cell to type in it. <kbd>Enter</kbd> keeps the value and <kbd>Escape</kbd> gives it up.

- **Duration**: working days; 0 makes the task a milestone.
- **Start**: the task starts no earlier than that day (a constraint, see below).
- **Finish**: the duration becomes the working days from the start to that day.
- **Predecessors**: as in MS Project, row numbers separated by commas, each with a type and a lag: `3`, `5SS`, `7FF+2`, `2FS-1d`.

The keyboard works as in a table: <kbd>↑</kbd> and <kbd>↓</kbd> move the selection, <kbd>Insert</kbd> adds a task before the selected one (<kbd>Shift</kbd>+<kbd>Insert</kbd> a milestone), <kbd>Delete</kbd> deletes the selected tasks with what is under them, <kbd>Tab</kbd> indents and <kbd>Shift</kbd>+<kbd>Tab</kbd> outdents, <kbd>F2</kbd> renames.

### Editing on the chart

- **Drag a bar** to move the task. It then starts no earlier than the day you drop it on. The date shows while you drag.
- **Drag a bar's end** to change its duration. The working days show while you drag.
- **Link two tasks**: hover over a bar, then drag from the small circle at one of its ends onto another bar. Which ends you join gives the type: from the finish to the other's start is finish-to-start (FS), start to start SS, finish to finish FF, start to finish SF.
- **Click an arrow** to change the link's type and lag (in working days; negative is an overlap), or to remove it.

Every change is one step to undo.

## A task in Properties

![A task in Properties](shots/planning-task.webp)

Select a task and Properties shows it: its name, its IFC kind, its duration, whether it is a milestone, its dates, and:

- **Constraint**: **As soon as possible** (the default), **Start no earlier than** a date, or **Must start on** a date. A task that must start before its predecessors finish takes float away from them. Their float then turns negative and they become critical.
- **Wet works**: concrete, screeds, plaster. A wet task does not work in the calendar's winter break, when the break is on.
- **Total float** and **Free float**: how many working days the task can slip without moving the finish, or without moving what follows it. **Late start** and **Late finish** are the latest dates that keep the finish.
- **Predecessors**: each link in, with its type and lag to change, or removed. **Successors** lists what follows the task.

## The critical path

Lapin works out the schedule after every change. A forward pass finds the earliest dates and a backward pass the latest. The tasks with no float make the critical path, and a delay to any of them moves the finish. A link to a summary holds every task under it. A link from a summary starts at its first task's start and ends at its last task's finish. Lapin refuses a link that would make a loop.

## The calendar

![The work calendar](shots/planning-calendar.webp)

**Planning → Calendar…** sets:

- the **working days** of the week (Monday to Friday by default) and the **hours** of a day (8 from 08:00);
- the **holidays**: Romania's legal holidays, worked out for every year (Orthodox Good Friday, Easter and Pentecost included), or none. The list under them shows a year's holidays to check;
- the **winter break** for wet works, off by default: from 15 December to 1 March unless you set other days;
- **exceptions**: days off for the project (a site closed), or days worked that would not be (a Saturday).

Durations and lags count working days of this calendar.

## Exchanging the schedule

- **Export MS Project…** writes an MS Project XML file. MS Project, ProjectLibre and GanttProject open it, and Primavera P6 imports it. It holds the tasks with their WBS, durations, dates, constraints and floats, the links with their types and lags, and the calendar with every holiday as an exception. **Import MS Project…** reads such a file in place of the project's schedule. A schedule written by Lapin and read again keeps its tasks' ids and dates.
- **Export IFC…** (also under **File**) writes the schedule into the IFC file with the model: an IfcWorkSchedule, its IfcTasks with their times, the IfcRelSequence links and the IfcWorkCalendar. Importing an IFC file that has a schedule brings it in too.
- **Export XLSX** writes a workbook: the Gantt's table and the calendar's days off.

Lapin's own fields that other programs have no place for (a task's constraint, whether it is wet, the winter break) travel in IFC as the property sets `Lapin_Task` and `Lapin_Calendar`. MS Project has no wet tasks, so the winter break stays in Lapin.

## What comes next

Linking the model's elements to tasks, a schedule generated from the model, the 4D simulation of the building growing in 3D, progress and baselines, and the cost estimate come in the next stages.
