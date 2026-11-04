/**
 * Planning (4D, docs/plan-4d5d.md, stages 1–3): the work schedule — tasks in a WBS, milestones, links with lags, the work
 * calendar with Romania's legal holidays, the critical path the engine works out — shown and edited in the Gantt, a
 * view under the 3D or in a window of its own; the model's elements in the tasks, found by each task's rule with its
 * exceptions (the selection dragged onto a row, TA), parts of layered elements in tasks of their own, the selection one
 * between the Gantt and the views; IFC processes, MS Project XML and a workbook to exchange it; the generator (stage 3):
 * a schedule made from a template's sequence, the model's storeys and elements and the user's norms, in one step; the 4D
 * simulation (stage 4): the model as the schedule has it on a day, played from the Gantt's date line, State at date on
 * views and sheets, the stages sheet, the Gantt on sheets, the video; progress (stage 5): the status date, the elements
 * marked done or started in 3D or in a plan (from a phone too, as a guest of the shared session), the percentages and
 * actual dates, the baseline and its variances, the progress read back from a workbook.
 */

import { CalendarDays, ChartGantt, CheckCheck, CircleOff, CirclePlay, Clapperboard, Diamond, FileVideo, Flag, FlagOff, LayoutPanelTop, Sheet, FileDown, FileSpreadsheet, FileUp, IndentDecrease, IndentIncrease, Link2, ListPlus, Plus, SearchX, Unlink2, Wand2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { exportIfc } from '../../engine/files';
import { CalendarDialog } from './CalendarDialog';
import { GenerateDialog } from './GenerateDialog';
import { GanttView } from './GanttView';
import { TaskSection } from './TaskSection';
import { AssignDialog } from './AssignDialog';
import { SelectionToTask } from './SelectionToTask';
import { ElementTasksSection, inSomeTask, TaskLinksSection } from './TaskLinksSection';
import { ganttOnSheet, SimulationCard, SimulationLooks, toggleSimulation, ViewDateSection } from './SimulationView';
import { StagesDialog, VideoDialog } from './SimulationDialogs';
import { addTask, assignSelection, clearBaseline, exportMsp, exportXlsx, importMsp, importProgress, indent, linkSelected, markSelection, openGantt, outdent, saveBaseline, selectUnlinked, taskElementsOf, unlinkSelected } from './schedule';

const planning: LapinModule = {
  id: 'planning',
  name: 'Planning',
  summary: 'The work schedule: a Gantt with its WBS, links and critical path on a calendar with Romania\'s holidays; the 4D simulation; IFC and MS Project exchange.',
  tabs: [{
    id: 'planning', label: 'Planning', i18nCtx: 'schedule', order: 84,
    groups: [
      {
        label: 'Work schedule', tools: [
          { id: 'planning.generate', label: 'Generate schedule…', icon: Wand2, run: () => useUi.setState({ dialog: 'planning.generate' }) },
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
        label: 'Progress', tools: [
          { id: 'planning.markDone', label: 'Mark done', icon: CheckCheck, shortcut: 'MD', run: () => void markSelection('done') },
          { id: 'planning.markStarted', label: 'Mark started', icon: CirclePlay, shortcut: 'MS', run: () => void markSelection('started') },
          { id: 'planning.saveBaseline', label: 'Save baseline', icon: Flag, run: () => void saveBaseline() },
        ],
        small: [
          { id: 'planning.unmark', label: 'Mark not started', icon: CircleOff, run: () => void markSelection('none') },
          { id: 'planning.clearBaseline', label: 'Clear baseline', icon: FlagOff, run: () => void clearBaseline() },
          { id: 'planning.importProgress', label: 'Import progress…', icon: FileUp, run: () => void importProgress() },
        ],
      },
      {
        label: '4D simulation', tools: [
          { id: 'planning.simulate', label: 'Simulate', icon: Clapperboard, run: () => void toggleSimulation() },
          { id: 'planning.stages', label: 'Stages sheet…', icon: LayoutPanelTop, run: () => useUi.setState({ dialog: 'planning.stages' }) },
        ],
        small: [
          { id: 'planning.ganttSheet', label: 'Gantt on sheet', icon: Sheet, run: () => void ganttOnSheet() },
          { id: 'planning.video', label: 'Export video…', icon: FileVideo, run: () => useUi.setState({ dialog: 'planning.video' }) },
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
  dialogs: { 'planning.calendar': CalendarDialog, 'planning.assign': AssignDialog, 'planning.generate': GenerateDialog, 'planning.stages': StagesDialog, 'planning.video': VideoDialog },
  background: SimulationLooks,
  propertySections: [
    { id: 'task', label: 'Task', classes: ['IfcTask'], component: TaskSection, replacesFields: () => true },
    { id: 'task-elements', label: 'Elements', classes: ['IfcTask'], component: TaskLinksSection },
    { id: 'element-tasks', label: 'Work schedule', classes: ['*'], shows: inSomeTask, component: ElementTasksSection },
    { id: 'view-date', label: '4D simulation', classes: ['LapinView'], shows: (n) => ['plan', 'section', 'elevation', '3d'].includes(String(n.props.kind)), component: ViewDateSection },
  ],
  expandSelection: taskElementsOf,
  overlays: [SelectionToTask, SimulationCard],
};

export default planning;
