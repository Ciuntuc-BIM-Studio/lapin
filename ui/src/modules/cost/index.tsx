/** Cost: quantities from the model, the bill of quantities (deviz), export. */

import { FileSpreadsheet, Sheet, Table2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

const cost: LapinModule = {
  id: 'cost',
  name: 'Cost',
  summary: 'Quantities, the Romanian bill of quantities (deviz), XLSX export.',
  tabs: [{
    id: 'quantities', label: 'Quantities', order: 50,
    groups: [
      { label: 'Take-off', tools: [{ id: 'schedule', label: 'Schedule', icon: Table2 }] },
      { label: 'Estimate', tools: [{ id: 'deviz', label: 'Bill of quantities', icon: Sheet }, { id: 'xlsx', label: 'Export XLSX', icon: FileSpreadsheet }] },
    ],
  }],
};

export default cost;
