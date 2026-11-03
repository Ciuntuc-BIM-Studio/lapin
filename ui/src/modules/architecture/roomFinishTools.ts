/** The Finishes tab's Room tools (decision 265): Finish room (a click in a room lays the chosen finish), the configurator, the schedule. */

import { PaintRoller, Settings2, Table2 } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { newElement } from './NewElement';

const TYPE = 'LapinRoomFinishType';
const chosenType = () => (useUi.getState().roomFinishTypeId ? { typeId: useUi.getState().roomFinishTypeId } : {});

/** The finish chosen for the tool, else the project's first, else the library's first brought in. */
async function typeToLay(): Promise<string | null> {
  const chosen = chosenType().typeId;
  if (chosen && useSession.getState().nodes.has(chosen)) return chosen;
  const first = ofClass(useSession.getState(), TYPE)[0];
  if (first) return first.id;
  const catalog = useSession.getState().info?.catalogs['architecture.roomFinishes'] as unknown as { library: { name: string }[] } | undefined;
  const r = await execute('architecture.roomFinishTypeFromLibrary', { name: catalog?.library[0]?.name ?? 'Living room' }) as { id?: string } | null;
  return r?.id ?? null;
}

/** A click in a room lays the chosen finish in it: its floor, walls and ceiling, its skirting and slab when it has them. */
export const finishRoom: ToolDef = {
  id: 'architecture.finishRoom', label: 'Finish room', icon: PaintRoller, shortcut: 'FR', properties: newElement(TYPE, 'roomFinishTypeId', 'roomFinishes'),
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const typeId = await typeToLay();
    if (typeId) await ctx.execute('architecture.applyRoomFinish', { storeyId: ctx.storeyId, x: p.x, y: p.y, typeId });
  },
};

export const roomFinishes: ToolDef = { id: 'architecture.roomFinishes', label: 'Room finishes', icon: Settings2, run: () => useUi.setState({ dialog: 'roomFinishes' }) };

export const roomFinishSchedule: ToolDef = {
  id: 'architecture.roomFinishSchedule', label: 'Finishes schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'roomFinishes' }).then((r) => { if (r) notify('Finishes schedule made: it is in the project browser, under Schedules.'); }),
};

