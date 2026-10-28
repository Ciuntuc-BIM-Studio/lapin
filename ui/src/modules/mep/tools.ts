/** What the MEP tools share with mep-pro's: the system a run goes in, a schedule tool. */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { ToolDef } from '../../platform/module';
import { Table2 } from 'lucide-react';
import { editedSystem } from './edit';
import { systemsOf } from './network';
import { setMep, useMep } from './prefs';

/** The system a run goes in: the one being edited, else the current one, else the only one; none: the Systems dialog. */
export async function runSystem(): Promise<string | null> {
  const nodes = useSession.getState().nodes;
  const edited = editedSystem();
  if (edited && nodes.has(edited)) return edited;
  const cur = useMep.getState().systemId;
  if (cur && nodes.has(cur)) return cur;
  const all = systemsOf(nodes);
  if (all.length === 1) { setMep({ systemId: all[0].id }); return all[0].id; }
  notify('Choose or make a system first (Systems…).');
  useUi.setState({ dialog: 'mepSystems' });
  return null;
}

/** A schedule the module offers (an IScheduleSource), made and opened. */
export const schedule = (id: string, label: string, kind: string): ToolDef => ({
  id, label, icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind }).then((r) => { const made = r as { id: string } | null; if (made?.id) useUi.setState({ view: 'schedule', scheduleId: made.id, selection: made.id, selected: [made.id] }); }),
});
