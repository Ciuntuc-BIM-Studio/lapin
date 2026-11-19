/**
 * Wood (docs/plan-wood.md, decision 239): timber-frame walls from the configurator, stage 1. The Wood tab opens the timber
 * frame configurator (a wall type's studs, plates, noggins and its openings' rules, with a framed sample wall) and makes the
 * Timber schedule (every member, the roofs' too); the properties panel shows a framed wall's members and a door's or
 * window's framing, with its own rule.
 */

import { Home, Settings2, Table2 } from 'lucide-react';
import type { Grip, GripView, LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { OpeningWoodSection, RoofWoodSection, WallWoodSection } from './Sections';
import { lazyPart } from '../../platform/lazyPart';
import { createScheduleOf } from '../../shell/fieldSchedule';

// Fetched the first time each is opened (platform/lazyPart.tsx).
const FrameDialog = lazyPart(() => import('./FrameDialog').then((m) => m.FrameDialog), { title: 'Timber frame' });

const frame: ToolDef = { id: 'wood.frame', label: 'Timber frame and CLT', icon: Settings2, run: () => useUi.setState({ dialog: 'woodFrame' }) };
const roofFraming: ToolDef = {
  id: 'wood.roofFraming', label: 'Roof framing', icon: Home,
  run: () => {
    const { selected } = useUi.getState();
    const { nodes } = useSession.getState();
    let roof = selected.map((id) => nodes.get(id)).find((n) => n?.type === 'IfcRoof');
    if (!roof) {
      const roofs = [...nodes.values()].filter((n) => n.type === 'IfcRoof');
      if (roofs.length === 1) roof = roofs[0];
    }
    if (!roof) { notify('Select a roof first, then open Roof framing.'); return; }
    useUi.setState({ dialog: 'roof', roofId: roof.id, roofDialogTab: 'framing' });
  },
};
const roofTimber: ToolDef = {
  id: 'wood.roofTimber', label: 'Roof timber', icon: Table2,
  run: () => void createScheduleOf('roofTimber').then((r) => { if (r) notify('Roof timber cutting list made: it is in the project browser, under Schedules.'); }),
};
const boards: ToolDef = {
  id: 'wood.boards', label: 'Boards schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'boards' }).then((r) => { if (r) notify('Boards schedule made: it is in the project browser, under Schedules.'); }),
};
const cltSchedule: ToolDef = {
  id: 'wood.clt', label: 'CLT panels', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'clt' }).then((r) => { if (r) notify('CLT panel schedule made: it is in the project browser, under Schedules.'); }),
};
const panels: ToolDef = {
  id: 'wood.panels', label: 'Wall panels', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'panels' }).then((r) => { if (r) notify('Wall panel schedule made: it is in the project browser, under Schedules.'); }),
};
const schedule: ToolDef = {
  id: 'wood.schedule', label: 'Timber schedule', icon: Table2,
  run: () => void createScheduleOf('timber').then((r) => { if (r) notify('Timber schedule made: it is in the project browser, under Schedules.'); }),
};

/**
 * A timber-frame wall's grips in its frame elevation (audit §14), as the engine drew them (the mark's frameGrips): each stud
 * dragged along the wall (Ctrl/⌘-click takes it out), each opening's outer king and outer jack dragged out or in to give it
 * more or fewer (its own rule). Each drag is one engine command (wood.frameGrip), one undo step.
 */
export function frameGrips(node: NodeDto, view?: GripView): Grip[] {
  const data = view?.mark?.data as { frameGrips?: { key: string; at: number[] }[] } | undefined;
  const viewId = view?.view;
  if (!data?.frameGrips || !viewId) return [];
  const send = (key: string, extra: Record<string, unknown>) => ({ command: 'wood.frameGrip', args: { id: node.id, viewId, key, ...extra } });
  return data.frameGrips.map((g) => {
    const stud = g.key.startsWith('stud:');
    return {
      key: `frame:${g.key}`, at: { x: g.at[0], y: g.at[1] }, kind: 'move' as const, view: viewId,
      title: stud ? 'Drag: move the stud along the wall (Ctrl-click: take it out)' : g.key.startsWith('king:') ? 'Drag out or in: more or fewer king studs' : 'Drag out or in: more or fewer jack studs',
      step: (to) => send(g.key, { point: [to.x, to.y] }),
      ...(stud ? { remove: () => send(g.key, { remove: true }) } : {}),
    };
  });
}

export const woodModule: LapinModule = {
  id: 'wood',
  name: 'Wood',
  summary: 'Timber-frame walls from the configurator: studs on a module, plates, noggins, and every door and window framed by its rules; the Timber schedule.',
  tabs: [{
    id: 'wood', label: 'Wood', order: 33, include: ['modify'],
    groups: [
      { label: 'Frame', tools: [frame, roofFraming] },
      { label: 'Schedule', tools: [schedule, roofTimber, boards, panels, cltSchedule] },
    ],
  }],
  dialogs: { woodFrame: FrameDialog },
  grips: { IfcWall: frameGrips },
  propertySections: [
    { id: 'woodWall', label: 'Wood', classes: ['IfcWall'], component: WallWoodSection },
    { id: 'woodOpening', label: 'Wood', classes: ['IfcDoor', 'IfcWindow'], component: OpeningWoodSection },
    { id: 'woodRoof', label: 'Wood', classes: ['IfcRoof'], component: RoofWoodSection },
  ],
};

export default woodModule;
