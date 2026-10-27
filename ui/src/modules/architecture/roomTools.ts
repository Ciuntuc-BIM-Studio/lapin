/** The room tools: a room by a click inside walls, or by its outline; rooms brought up to date; the room colours; a colour legend. */

import { Palette, RefreshCw, Shapes, SquareDashed } from 'lucide-react';
import { RoomIcon } from '../../icons';
import type { ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute } from '../../engine/session';
import { frontView } from '../../viewport/frontView';

/** A room: a click inside the walls, curtain walls or columns that enclose it; its outline follows them. */
export const room: ToolDef = {
  id: 'architecture.room', label: 'Room', icon: RoomIcon, shortcut: 'RM',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    await ctx.execute('architecture.addRoom', { storeyId: ctx.storeyId, x: p.x, y: p.y });
  },
};

/** A room drawn: its corners, then the first one again (or Enter); it keeps the outline drawn (a terrace, an open-plan area). */
export const roomOutline: ToolDef = {
  id: 'architecture.roomOutline', label: 'Room by outline', icon: SquareDashed,
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    await ctx.execute('architecture.addRoom', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) });
  },
};

/** The outlines the storey's rooms find now, stored on them (what they keep should their walls go). */
export const updateRooms: ToolDef = {
  id: 'architecture.updateRooms', label: 'Update rooms', icon: RefreshCw,
  run: () => {
    const storeyId = activeStorey()?.id;
    void (execute('architecture.updateRooms', storeyId ? { storeyId } : {}) as Promise<{ updated?: number; lost?: number } | null>).then((r) => {
      if (!r) return;
      notify(r.lost ? `${r.updated ?? 0} room(s) updated; ${r.lost} no longer enclosed (they keep their last outline).` : `${r.updated ?? 0} room(s) updated.`);
    });
  },
};

/** The quick configurator of room colours and hatches. */
export const roomColors: ToolDef = { id: 'architecture.roomColors', label: 'Room colours', icon: Palette, run: () => useUi.setState({ dialog: 'roomColors' }) };

/** A colour legend: click where it goes on the plan; it lists the colours the plan's rooms use. */
export const colorLegend: ToolDef = {
  id: 'architecture.colorLegend', label: 'Colour legend', icon: Shapes,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }) => {
    const viewId = await frontView();
    if (!viewId || viewId === '3d') { notify('A colour legend goes on a plan: open one.'); return; }
    await execute('architecture.addColorLegend', { viewId, x: p.x, y: p.y });
  },
};
