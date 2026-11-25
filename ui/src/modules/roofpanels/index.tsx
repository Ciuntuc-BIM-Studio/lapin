/**
 * Roof panel layout (docs/plan-roof-panels.md, decision 848), stage S2: what the interface shows of the engine module
 * `roofpanels`. A roof's Panels section (its profile, colour, longest panel, what to buy, the warnings); Panel layout…
 * on the Architecture tab and in the section (the roof in plan or laid flat, the offcuts reused, the tables); the
 * Roof panels schedule (a shipped field schedule of the panels). The panels themselves are the covering's geometry,
 * drawn by the engine in 3D, plans and sections.
 */

import { LayoutGrid, Table2 } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { lazyPart } from '../../platform/lazyPart';
import { PanelsSection } from './PanelsSection';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const PanelLayoutDialog = lazyPart(() => import('./PanelLayoutDialog').then((m) => m.PanelLayoutDialog), { title: 'Panel layout' });

/** The roof Panel layout opens on: the one selected, else the project's only roof. */
export function roofFor(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto>): NodeDto | null {
  const chosen = selected.map((id) => nodes.get(id)).find((n) => n?.type === 'IfcRoof');
  if (chosen) return chosen;
  const roofs = [...nodes.values()].filter((n) => n.type === 'IfcRoof');
  return roofs.length === 1 ? roofs[0] : null;
}

const layout: ToolDef = {
  id: 'roofpanels.layout', label: 'Panel layout…', icon: LayoutGrid, alsoCalled: ['roof panels', 'metal tiles', 'panotaj'],
  tooltip: 'The roof\'s metal tiles or panels: the layout on its slopes, what to buy, the offcuts reused',
  run: () => {
    const roof = roofFor(useUi.getState().selected, useSession.getState().nodes);
    if (!roof) { notify('Select a roof first, then open Panel layout.'); return; }
    useUi.setState({ dialog: 'roofPanelLayout', roofId: roof.id });
  },
};

const schedule: ToolDef = {
  id: 'roofpanels.schedule', label: 'Roof panels', icon: Table2, tooltip: 'A schedule of the roofs\' panels: by length, bought or cut from an offcut, with the sheet bought',
  run: () => void execute('architecture.importSchedule', { builtin: 'roofPanels' }).then((r) => { if (r) notify('Roof panels schedule made: it is in the project browser, under Schedules.'); }),
};

const sloped = (n: NodeDto) => !['flat', 'barrel'].includes(String(n.props.shape ?? ''));

const roofpanels: LapinModule = {
  id: 'roofpanels',
  name: 'Roof panel layout',
  summary: 'Metal tiles and standing-seam panels laid out on the roofs: what to buy by length, the offcuts reused in the valleys, the panels drawn as the covering.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 20,
    groups: [{ label: 'Roof panels', after: 'Build', tools: [layout], small: [schedule] }],
  }],
  dialogs: { roofPanelLayout: PanelLayoutDialog },
  propertySections: [
    { id: 'roofPanels', label: 'Panels', classes: ['IfcRoof'], shows: sloped, component: PanelsSection },
  ],
};

export default roofpanels;
