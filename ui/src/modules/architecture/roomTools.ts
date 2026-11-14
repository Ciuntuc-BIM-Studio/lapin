/** The room tools: a room by a click inside walls, or by its outline; room separation lines; rooms brought up to date; the room colours; a colour legend. */

import { Circle, Minus, Palette, RectangleHorizontal, RefreshCw, Shapes, Spline, SquareDashed, SquareSplitHorizontal, Tag, Waypoints } from 'lucide-react';
import { RoomIcon } from '../../icons';
import type { ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { openPulldown } from '../../shell/pulldown';
import type { ToolContext, Vec2 } from '../../platform/module';

/**
 * A room the next Room or Room by outline gives its outline to instead of
 * making a new one (a room imported from IFC without a shape): set from its
 * properties, used while it is still the one selected, then forgotten.
 */
export const outlineFor: { id: string | null } = { id: null };
function target(): string | null {
  const id = outlineFor.id;
  outlineFor.id = null;
  return id && useUi.getState().selected.includes(id) ? id : null;
}

/** A room: a click inside the walls, curtain walls or columns that enclose it; its outline follows them. */
export const room: ToolDef = {
  id: 'architecture.room', label: 'Room', icon: RoomIcon, shortcut: 'RM',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const id = target();
    if (id) await ctx.execute('architecture.updateRoom', { id, x: p.x, y: p.y });
    else await ctx.execute('architecture.addRoom', { storeyId: ctx.storeyId, x: p.x, y: p.y });
  },
};

/** A room drawn: its corners, then the first one again (or Enter); it keeps the outline drawn (a terrace, an open-plan area). */
export const roomOutline: ToolDef = {
  id: 'architecture.roomOutline', label: 'Room by outline', icon: SquareDashed,
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    const id = target();
    if (id) await ctx.execute('architecture.updateRoom', { id, points: points.map((p) => [p.x, p.y]) });
    else await ctx.execute('architecture.addRoom', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) });
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

/** Room tag…: what the rooms' stamps show, line by line. */
export const roomTag: ToolDef = { id: 'architecture.roomTag', label: 'Room tag', icon: Tag, run: () => useUi.setState({ dialog: 'roomTag' }) };

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

// ─── Room separation lines (forum topic 80) ────────────────────────────────

/** A room separation line of a kind, from the points drawn, on the plan's storey: the rooms around it find their outlines again. */
const divider = (ctx: ToolContext, kind: 'line' | 'polyline' | 'rectangle' | 'arc' | 'circle', points: Vec2[], closed = false) =>
  ctx.execute('architecture.addRoomDivider', { storeyId: ctx.storeyId, kind, points: points.map((p) => [p.x, p.y]), ...(closed ? { closed: true } : {}) });

/** A straight room separation line (chained): it bounds rooms as a wall would, with no body (an open-plan kitchen and living room). */
export const roomDivider: ToolDef = {
  id: 'architecture.roomDivider', label: 'Room separation line', icon: Minus, shortcut: 'RS',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => { await divider(ctx, 'line', [a, b]); },
};
/** A room separation polyline: its corners, then Enter (or a double click). */
export const roomDividerPolyline: ToolDef = {
  id: 'architecture.roomDividerPolyline', label: 'Room separation polyline', icon: Waypoints,
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { if (points.length >= 2) await divider(ctx, 'polyline', points); },
};
/** A room separation rectangle: two opposite corners; the zone inside it is a room of its own. */
export const roomDividerRectangle: ToolDef = {
  id: 'architecture.roomDividerRectangle', label: 'Room separation rectangle', icon: RectangleHorizontal,
  gesture: { kind: 'rectangle' },
  complete: async ({ points: [a, b] }, ctx) => { await divider(ctx, 'rectangle', [a, b]); },
};
/** A room separation arc: its start, its end, then a point it passes through. */
export const roomDividerArc: ToolDef = {
  id: 'architecture.roomDividerArc', label: 'Room separation arc', icon: Spline,
  gesture: { kind: 'threePoints', arc: true },
  complete: async ({ points: [a, b, p] }, ctx) => { await divider(ctx, 'arc', [a, b, p]); },
};
/** A room separation circle: its centre, then a point on it. */
export const roomDividerCircle: ToolDef = {
  id: 'architecture.roomDividerCircle', label: 'Room separation circle', icon: Circle,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [c, p] }, ctx) => { await divider(ctx, 'circle', [c, p]); },
};
/** The room separation tools, one button: a line, a polyline, a rectangle, an arc or a circle. */
export const roomDividers: ToolDef = {
  id: 'architecture.roomDividers', label: 'Room separation', icon: SquareSplitHorizontal,
  tooltip: 'Lines that bound rooms as walls would, with no body: each zone a room of its own, with its own area',
  items: [roomDivider, roomDividerPolyline, roomDividerRectangle, roomDividerArc, roomDividerCircle],
  run: () => openPulldown(roomDividers),
};
