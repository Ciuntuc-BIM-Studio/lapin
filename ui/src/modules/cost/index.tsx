/** Cost: quantities from the model, the bill of quantities (deviz), export. */

import { FileSpreadsheet, Sheet, Table2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

function Quantities() {
  return (
    <dl className="fields">
      <dt>Net side area</dt><dd className="num">12.40 m²</dd>
      <dt>Net volume</dt><dd className="num">3.72 m³</dd>
      <dt>Blocks</dt><dd className="num">130 pcs</dd>
      <dt>Mortar</dt><dd className="num">0.31 m³</dd>
    </dl>
  );
}

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
  propertySections: [{ id: 'quantities', label: 'Quantities', kinds: ['wall', 'slab'], component: Quantities }],
};

export default cost;
