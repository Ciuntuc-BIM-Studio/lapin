/**
 * Planning (4D, docs/plan-4d5d.md, stages 1–2): the work schedule — tasks in a WBS, milestones, links with lags, the work
 * calendar with Romania's legal holidays, the critical path the engine works out — shown and edited in the Gantt, a
 * view under the 3D or in a window of its own; the model's elements in the tasks, found by each task's rule with its
 * exceptions (the selection dragged onto a row, TA), parts of layered elements in tasks of their own, the selection one
 * between the Gantt and the views; IFC processes, MS Project XML and a workbook to exchange it. The generator and the 4D
 * simulation follow in later stages.
 */

import { CalendarDays, ChartGantt, Diamond, FileDown, FileSpreadsheet, FileUp, IndentDecrease, IndentIncrease, Link2, ListPlus, Plus, SearchX, Unlink2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { exportIfc } from '../../engine/files';
import { CalendarDialog } from './CalendarDialog';
import { GanttView } from './GanttView';
import { TaskSection } from './TaskSection';
import { AssignDialog } from './AssignDialog';
import { SelectionToTask } from './SelectionToTask';
import { ElementTasksSection, inSomeTask, TaskLinksSection } from './TaskLinksSection';
import { addTask, assignSelection, exportMsp, exportXlsx, importMsp, indent, linkSelected, openGantt, outdent, selectUnlinked, taskElementsOf, unlinkSelected } from './schedule';

const planning: LapinModule = {
  id: 'planning',
  name: 'Planning',
  summary: 'The work schedule: a Gantt with its WBS, links and critical path on a calendar with Romania\'s holidays; IFC and MS Project exchange.',
  tabs: [{
    id: 'planning', label: 'Planning', i18nCtx: 'schedule', order: 84,
    groups: [
      {
        label: 'Work schedule', tools: [
          { id: 'planning.gantt', label: 'Gantt', icon: ChartGantt, run: openGantt },
          { id: 'planning.calendar', label: 'Calendar…', icon: CalendarDays, run: () => useUi.setState({ dialog: 'planning.calendar' }) },
        ],
      },
      {
        label: 'Tasks', tools: [
          { id: 'planning.task', label: 'Task', icon: Plus, run: () => { openGantt(); void addTask(false); } },
          { id: 'planning.milestone', label: 'Milestone', icon: Diamond, run: () => { openGantt(); void addTask(true); } },
        ],
        small: [
          { id: 'planning.indent', label: 'Indent', icon: IndentIncrease, run: indent },
          { id: 'planning.outdent', label: 'Outdent', icon: IndentDecrease, run: outdent },
          { id: 'planning.link', label: 'Link tasks', icon: Link2, run: linkSelected },
          { id: 'planning.unlink', label: 'Unlink tasks', icon: Unlink2, run: unlinkSelected },
        ],
      },
      {
        label: 'Elements', tools: [
          { id: 'planning.assign', label: 'Add to task…', icon: ListPlus, shortcut: 'TA', run: assignSelection },
        ],
        small: [
          { id: 'planning.unlinked', label: 'Elements without a task', icon: SearchX, run: () => void selectUnlinked() },
        ],
      },
      {
        label: 'Exchange', tools: [
          { id: 'planning.importMsp', label: 'Import MS Project…', icon: FileUp, run: () => void importMsp() },
          { id: 'planning.exportMsp', label: 'Export MS Project…', icon: FileDown, run: () => void exportMsp() },
        ],
        small: [
          { id: 'planning.exportXlsx', label: 'Export XLSX', icon: FileSpreadsheet, run: () => void exportXlsx() },
          { id: 'planning.exportIfc', label: 'Export IFC…', icon: FileDown, run: () => void exportIfc() },
        ],
      },
    ],
  }],
  views: { gantt: { label: 'Gantt', component: GanttView } },
  dialogs: { 'planning.calendar': CalendarDialog, 'planning.assign': AssignDialog },
  propertySections: [
    { id: 'task', label: 'Task', classes: ['IfcTask'], component: TaskSection, replacesFields: () => true },
    { id: 'task-elements', label: 'Elements', classes: ['IfcTask'], component: TaskLinksSection },
    { id: 'element-tasks', label: 'Work schedule', classes: ['*'], shows: inSomeTask, component: ElementTasksSection },
  ],
  expandSelection: taskElementsOf,
  overlays: [SelectionToTask],
};

export default planning;
