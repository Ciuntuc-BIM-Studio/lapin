/**
 * Wood (docs/plan-wood.md, decision 239): timber-frame walls from the configurator, stage 1. The Wood tab opens the timber
 * frame configurator (a wall type's studs, plates, noggins and its openings' rules, with a framed sample wall) and makes the
 * Timber schedule (every member, the roofs' too); the properties panel shows a framed wall's members and a door's or
 * window's framing, with its own rule.
 */

import { Settings2, Table2 } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { FrameDialog } from './FrameDialog';
import { OpeningWoodSection, WallWoodSection } from './Sections';

const frame: ToolDef = { id: 'wood.frame', label: 'Timber frame', icon: Settings2, run: () => useUi.setState({ dialog: 'woodFrame' }) };
const schedule: ToolDef = {
  id: 'wood.schedule', label: 'Timber schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'timber' }).then((r) => { if (r) notify('Timber schedule made: it is in the project browser, under Schedules.'); }),
};

export const woodModule: LapinModule = {
  id: 'wood',
  name: 'Wood',
  summary: 'Timber-frame walls from the configurator: studs on a module, plates, noggins, and every door and window framed by its rules; the Timber schedule.',
  tabs: [{
    id: 'wood', label: 'Wood', order: 33, include: ['modify'],
    groups: [
      { label: 'Frame', tools: [frame] },
      { label: 'Schedule', tools: [schedule] },
    ],
  }],
  dialogs: { woodFrame: FrameDialog },
  propertySections: [
    { id: 'woodWall', label: 'Wood', classes: ['IfcWall'], component: WallWoodSection },
    { id: 'woodOpening', label: 'Wood', classes: ['IfcDoor', 'IfcWindow'], component: OpeningWoodSection },
  ],
};

export default woodModule;
