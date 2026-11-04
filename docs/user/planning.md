---
title: Planning
order: 97
summary: The work schedule in a Gantt chart: generated from the model or made by hand, tasks in a WBS, milestones, links with lags, the critical path worked out on a calendar with Romania's legal holidays; to and from MS Project, IFC and Excel.
---

## The Planning tab

![The Planning tab](shots/ribbon-planning.webp)

- **Generate schedule…**: a whole schedule made from the model in one step (below).
- **Gantt**: opens the schedule under the view. The screen splits into two rows, so you see the model above and the schedule below.
- **Calendar…**: the working days and hours, the holidays and the winter break (below).
- **Task** and **Milestone**: a new task or milestone before the task selected in the Gantt (or at the end).
- **Indent**, **Outdent**, **Link** and **Unlink**: the WBS and the links of the tasks selected.
- **Add to task…** (TA) and **Elements without a task**: the model's elements in the tasks (below).
- **Import MS Project…**, **Export MS Project…**, **Export XLSX** and **Export IFC…**: the schedule to and from other programs (below).

A project has one schedule. It is made the first time you add a task or generate one, and starts the next Monday. Change its start in the Gantt's toolbar.

## Generating the schedule from the model

![Generate schedule: the templates, the Start tab and the preview](shots/planning-generate.webp)

**Planning → Generate schedule…** (also in the Gantt's toolbar) makes a complete schedule from the model: a task for each step of the works, and for each storey where the step goes storey by storey, every task already holding its elements. On the left are the **templates**: **Concrete frame**, **Load-bearing masonry**, **Steel hall** and **Timber frame**. Lapin marks the one the model asks for as *suggested*: a steel hall when the model has one, a timber frame when its walls are framed, load-bearing masonry when it has no columns, else a concrete frame. Above them are the project's own styles (**Save as style…** keeps the spec you configured, for this project and the next you make from it).

On the right is the **preview**: the tasks the generator would make, as bars, each with the quantity that timed it, and under them the counts:

- **Elements linked**: the elements the tasks take out of all those that need a task. It is the most important number here. When some are left out, **Show the elements without a task** selects them in the view, and **A step for them** adds a step that takes their kinds.
- **Durations from norms** and **Durations by default (no norm)**: how many tasks a norm timed, and how many took their step's days.
- **Formwork and scaffolds on their hosts' tasks** (below).

**Apply** writes the schedule in one step to undo. The screen then splits: the model above, the Gantt below.

### The Start tab

- **Start**: the schedule's first day.
- **Split by storey**: a task a storey for the steps that go storey by storey (the structure, the masonry, the finishes); untick it for a task a step.
- **Demolition first**: the elements the phases demolish go into a **Demolition** task before all the others (its IFC kind DEMOLITION). Existing elements that stay are no task's.
- **Formwork** and **Scaffolding**: whether these temporary elements live by their hosts' tasks (below).
- **Keep the tasks made by hand**: when the schedule is generated again (below).

### The Sequence tab

![The Sequence tab: the steps in order](shots/planning-generate-sequence.webp)

The template's steps in order. Each one can be switched off, renamed and moved up or down with its arrows. It says whether it goes **once** for the building or **per storey**, and how it follows the step before it on the same storey: finish to start (FS), or start to start (SS) with a lag, for finishes that climb behind the structure. The pencil opens what the step builds: the kinds of elements, picked from those the model has, and the **Part** of them (whole, their core, or their finishes, as a task's part).

The links come from the order: each task follows the step before it on the same storey and the same step on the storey below. In load-bearing masonry and timber frames, a storey's walls also wait for the floor of the storey below.

Each task is made with a **rule** (its kinds, its storey), so it keeps its elements: a wall drawn after generating joins its task by itself, with no need to generate again.

### The Durations tab

![The Durations tab: the norms and each step's norm, crew and days](shots/planning-generate-durations.webp)

A task's duration comes from its elements' quantity (from the quantity takeoff: the volume of the core, the area of the finishes, the doors and windows by the piece, the steel by its weight) and a **productivity norm**:

*duration = quantity × man-hours per unit ÷ (crew × hours a day)*, in whole working days. For example 4.5 m³ of masonry at 4 man-hours a cubic metre, with a crew of 2 working 8 hours, is 18 ÷ 16 = 1.125, so 2 days.

The norms are yours. Lapin does not ship any norm book. **Import norms…** reads your table from a CSV or an XLSX file: a column for the code, one for the text, one for the unit (m3, mc, m2, mp, m, ml, buc, pcs, kg, t…), one for the man-hours per unit (or a productivity in units an hour), and optionally one for the crew. The headings may be in English or Romanian. **Template (CSV)** saves the columns with a few rows of rough values written by Lapin, each marked *indicative*. **Use Lapin's indicative values** puts those rows into the project to try the generator; the dialog then warns that the durations come from indicative values, not from a norm.

Under the norms, each step picks its **Norm**, its **Crew** (0: the norm's own) and its **Days**, used when no norm times it. The Calendar tab shows the calendar the durations count in; its hours a day divide the man-hours.

### Generated again

Open the generator again and **Apply**: the schedule is made again over the same tasks, so their ids stay. What you changed by hand stays:

- a generated task you edited (its name, duration, dates, rule, part) is **Kept as edited**, ticked in its Properties; untick it to have it made again from its step;
- the elements you added to a task or took out of it by hand stay its exceptions;
- the tasks and the links you made yourself stay; untick **Keep the tasks made by hand** to have only the generated ones.

![A generated task: its duration from the quantity and the norm](shots/planning-generated.webp)

In Properties a generated task says where its duration came from: the quantity, the norm and the crew, or the step's days when no norm timed it.

![A generated task in Properties](shots/planning-generated-task.webp)

### Formwork and scaffolds

A formwork or a scaffold from **Site works** has no task of its own: it lives by the tasks of what it serves. A formwork goes up when its floor, beam or wall starts being built, and is struck when the days set on its type have passed after it is finished (**Struck after**, in working days: 21 for floors and beams, 7 for walls, columns and footings unless the type says otherwise). A scaffold stands from the first task on its walls to the last, their finishes included. Select one and its Properties say when it is **On site** and which tasks it **Lives by**.

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

## The model's elements in the tasks

![The Masonry task selected: its walls drawn selected in 3D](shots/planning-links.webp)

A task takes elements of the model, so the schedule knows what is built when. Select a task and its elements are drawn selected in 3D and in the plans; double-click its bar to select them and frame them. Select elements in a view and the rows of their tasks are marked in the Gantt. The **Elements** column counts each task's elements (a summary's with those of the tasks under it).

### A rule

![A task's elements in Properties](shots/planning-task-elements.webp)

Under **Elements** in Properties, tick **Find them by a rule** and pick what the task takes, as in a schedule's filter: **Kinds**, **Storeys**, **Types**, **Materials**, **Groups** and **Phases**, each value with how many elements have it. Every condition you set must hold; one left empty lets everything through. The rule is kept, not the elements: a wall drawn tomorrow on the ground floor joins the task "Masonry ground floor" by itself. **Rule from the selection** makes a rule of the kinds and storeys of the elements selected.

### A part of the elements

**Part** says what the task takes of each element: the **whole elements**, their **core** (the structural layer), their **finishes** (every layer but the core), the layers of one material, or those of a material category (Finish, Insulation…). So a layered wall is built in a Masonry task (its core) and plastered in a Plaster task (its finishes). An element without layers is its own core and has no finishes.

### By hand

![The selection's card over the 3D, a wall's two tasks marked in the Gantt](shots/planning-element-tasks.webp)

While the Gantt is open, a card over the model view says how many elements are selected and which tasks they are in. Drag it by its handle onto a task's row: the elements go into that task. Hold <kbd>Shift</kbd> when you drop to take them out of it. **Add to task…** (<kbd>T</kbd> <kbd>A</kbd>, also on the Planning tab) picks the task from a list.

![Add to task](shots/planning-assign.webp)

What you add or take out by hand are the task's **exceptions**: the rule stays as it is. Properties counts them, and the × next to each forgets them. An element's own Properties lists its tasks under **Work schedule**, each with a × to take it out. Every change is one step to undo.

### Elements without a task

Once a task takes elements, the Gantt's toolbar says how many elements have no task, or have layers no task takes; a click selects them, ready to drag onto a task. **Planning → Elements without a task** does the same. **Manage → Check** lists them too, under *Elements with a task*: a warning for each, a click selecting it. Existing elements that stay need no task.

### In IFC and MS Project

The IFC export writes what each task builds as an IfcRelAssignsToProduct (the element, with the tasks building it), and what a demolition, removal or dismantling task acts on as an IfcRelAssignsToProcess. The rule, the part and the exceptions travel in `Lapin_Task`, so Lapin reads the rule back; a file from another program gives tasks holding the elements it assigns to them. MS Project XML carries them in the field Text1, named *Lapin elements*. The workbook has an **Elements** and a **Rule** column.

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

The 4D simulation of the building growing in 3D, progress and baselines, and the cost estimate come in the next stages.

