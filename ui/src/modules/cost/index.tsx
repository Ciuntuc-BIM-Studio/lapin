/**
 * Quantities: what the model measures, as schedules (decision 287, the tester's report #75) — the quantity takeoff
 * (every element's quantities by kind, type, storey or material), every other schedule the edition makes, the
 * takeoff as a workbook. Schedules are the Architecture module's; they go on sheets like views.
 */

import { FileSpreadsheet, Sigma, Table2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { SchedulesDialog } from './SchedulesDialog';
import { exportTakeoff, takeoff } from './schedules';

const cost: LapinModule = {
  id: 'cost',
  name: 'Cost',
  summary: 'Quantities from the model: the quantity takeoff, every schedule, XLSX export.',
  tabs: [{
    id: 'quantities', label: 'Quantities', order: 85,
    groups: [
      {
        label: 'Take-off', tools: [
          { id: 'cost.takeoff', label: 'Quantity takeoff', icon: Sigma, run: () => void takeoff() },
          { id: 'cost.schedules', label: 'Schedules…', icon: Table2, run: () => useUi.setState({ dialog: 'schedules' }) },
        ],
      },
      { label: 'Export', tools: [{ id: 'cost.xlsx', label: 'Export XLSX', icon: FileSpreadsheet, run: () => void exportTakeoff() }] },
    ],
  }],
  dialogs: { schedules: SchedulesDialog },
};

export default cost;
