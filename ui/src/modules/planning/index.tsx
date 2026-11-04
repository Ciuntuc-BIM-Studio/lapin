/**
 * Planning (4D, docs/plan-4d5d.md, stage 1): the work schedule — tasks in a WBS, milestones, links with lags, the work
 * calendar with Romania's legal holidays, the critical path the engine works out — shown and edited in the Gantt, a
 * view under the 3D or in a window of its own; IFC processes, MS Project XML and a workbook to exchange it. Linking
 * the model's elements, the generator and the 4D simulation follow in later stages.
 */

import { CalendarDays, ChartGantt, Diamond, FileDown, FileSpreadsheet, FileUp, IndentDecrease, IndentIncrease, Link2, Plus, Unlink2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { exportIfc } from '../../engine/files';
import { CalendarDialog } from './CalendarDialog';
import { GanttView } from './GanttView';
import { TaskSection } from './TaskSection';
import { addTask, exportMsp, exportXlsx, importMsp, indent, linkSelected, openGantt, outdent, unlinkSelected } from './schedule';

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
  dialogs: { 'planning.calendar': CalendarDialog },
  propertySections: [{ id: 'task', label: 'Task', classes: ['IfcTask'], component: TaskSection, replacesFields: () => true }],
};

export default planning;
