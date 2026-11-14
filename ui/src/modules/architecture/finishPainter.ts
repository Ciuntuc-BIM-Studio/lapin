/**
 * The finish painter (forum topic 83, docs/plan-finish-painter.md): an edit mode (the platform's edit scope, as MEP's systems
 * and Studio's parts) where the faces of walls, floors and columns are painted with finishes. Walls, floors, columns, beams and
 * the zones stay, the rest is faded. The palette (FinishPainterPanel) chooses the finish; the brush paints the region of a
 * face under the cursor (or the whole face), Split draws a rectangle or a line on a face in 3D (the direct sketch, its
 * shapes given here instead of to Studio), the eyedropper takes a region's finish, Clear takes it off. One command a stroke.
 */

import { Brush, Eraser, PaintBucket, Pipette, SquareDashed, Spline, Table2 } from 'lucide-react';
import { create } from 'zustand';
import type { ToolDef, ToolInput } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, query } from '../../engine/session';
import { enterScope, exitScope, useScope } from '../../platform/scope';
import { endDirect, startDirect, type DirectOwner } from '../../viewport/sketch/direct';
import { cancel as cancelCommand } from '../../viewport/cad/session';
import { toWorld } from '../../viewport/workPlane';

export const PAINTER = 'Finishes';
export const ZONE = 'LapinFinishZone';
/** What a brush lands on: the hosts whose faces are named, and the zones already on them (the stroke goes to their host). */
export const HOSTS = ['IfcWall', 'IfcSlab', 'IfcColumn', 'IfcBeam', ZONE];
const KEPT = new Set([...HOSTS, 'LapinFinishSplit']);

/** A finish to paint with: a type (a room finish's part with it), or layers of its own. */
export interface PaintFinish { name: string; typeId?: string; part?: string; layers?: { material: string; thickness: number }[]; library?: boolean }

/** A palette entry as the engine lists it (architecture.finishPalette). */
export interface PaletteEntry { key: string; name: string; kind: 'surface' | 'room'; typeId?: string; part?: string; use: string; library?: boolean; layers: { material: string; thickness: number }[] }

export const usePainter = create<{ finish: PaintFinish | null; whole: boolean }>(() => ({ finish: null, whole: false }));

export const painterOn = (): boolean => useScope.getState().scope?.kind === PAINTER;

/** The painter's edit mode: entered with the brush in hand; Done (Esc) leaves it, the tool put down. */
export function openPainter(): void {
  if (!painterOn()) {
    enterScope({
      id: 'finish-painter', kind: PAINTER, label: 'Finish painter',
      includes: (n: NodeDto) => KEPT.has(n.type),
      onDone: () => { endDirect(); if (useUi.getState().tool?.startsWith('architecture.paint')) useUi.setState({ tool: null }); },
    });
  }
  useUi.setState({ tool: paintBrush.id });
}

export const leavePainter = () => void exitScope();

/** The finish chosen, as the commands take it; a library entry brought into the project first. */
async function finishArgs(): Promise<Record<string, unknown> | null> {
  const f = usePainter.getState().finish;
  if (!f) { notify('Choose a finish in the palette first.'); return null; }
  if (f.library) {
    const r = await execute('architecture.surfaceFinishTypeFromLibrary', { name: f.name }) as { id?: string } | null;
    if (!r?.id) return null;
    usePainter.setState({ finish: { name: f.name, typeId: r.id } });
    return { finishType: r.id };
  }
  if (f.typeId) return { finishType: f.typeId, ...(f.part ? { part: f.part } : {}) };
  return { layers: f.layers ?? [], name: f.name };
}

/** Where a stroke landed, for the engine: the element and the point (with the triangle's normal in 3D). */
const hit = ({ entity, face, points }: ToolInput): Record<string, unknown> | null => {
  if (!entity) return null;
  const point = face?.point ?? (points[0] ? [points[0].x, points[0].y] : null);
  if (!point) return null;
  return { hostId: entity.id, point, ...(face?.normal ? { normal: face.normal } : {}) };
};

const faceGesture: ToolDef['gesture'] = { kind: 'face', types: HOSTS, query: 'architecture.finishFaceAt' };

/** The brush: the region under the cursor painted with the finish chosen (the whole face with Whole face on). */
export const paintBrush: ToolDef = {
  id: 'architecture.paintFinish', label: 'Brush', icon: Brush, gesture: faceGesture,
  complete: async (input) => {
    if (!painterOn()) openPainter();
    const at = hit(input);
    const finish = at && await finishArgs();
    if (!at || !finish) return;
    await execute('architecture.paintFinish', { ...at, ...finish, ...(usePainter.getState().whole ? { whole: true } : {}) });
  },
};

/** The eyedropper: the finish of the region under the cursor taken into the palette. */
export const paintEyedropper: ToolDef = {
  id: 'architecture.paintPick', label: 'Eyedropper', icon: Pipette, gesture: faceGesture,
  complete: async (input) => {
    const at = hit(input);
    if (!at) return;
    const r = await query<{ zoneId?: string; finish?: string; finishType?: string; part?: string; bare?: boolean; layers?: { material: string; thickness: number }[] } | null>(
      'architecture.finishFaceAt', { id: at.hostId, point: at.point, ...(at.normal ? { normal: at.normal } : {}) });
    if (!r?.zoneId || r.bare) { notify('Nothing is painted there.'); return; }
    usePainter.setState({ finish: r.finishType ? { name: r.finish ?? '', typeId: r.finishType, part: r.part } : { name: r.finish ?? '', layers: r.layers ?? [] } });
    notify('The finish is in the palette now.');
  },
};

/** Clear: the region under the cursor taken back to bare (the whole face with Whole face on; its splits stay). */
export const paintClear: ToolDef = {
  id: 'architecture.paintClear', label: 'Clear', icon: Eraser, gesture: faceGesture,
  complete: async (input) => {
    const at = hit(input);
    if (at) await execute('architecture.clearFinish', { ...at, ...(usePainter.getState().whole ? { all: true } : {}) });
  },
};

/** The direct sketch's shapes, drawn on a face, made splits of it. */
const splitOwner: DirectOwner = {
  faceQuery: 'architecture.finishFaceAt',
  store: async (p, o) => {
    if (!p.pick) { notify('Draw the split on a face of a wall, a floor or a column.'); return; }
    const w = (q: { x: number; y: number }) => { const v = toWorld(p.frame, q); return [v.x, v.y, v.z]; };
    const pts = o.kind === 'closed' ? o.outline : o.kind === 'open' ? o.pts : null;
    if (!pts || pts.length < 2) return;
    const r = await execute('architecture.splitFinishFace', { hostId: p.pick.id, point: p.pick.point, normal: p.pick.normal, points: pts.map(w), closed: o.kind === 'closed' });
    if (r) notify('The face is split: paint each part with the brush.');
  },
};

const splitTool = (id: string, label: string, icon: ToolDef['icon'], direct: 'rect' | 'line'): ToolDef => ({
  id, label, icon,
  run: () => {
    if (!painterOn()) openPainter();
    useUi.setState({ view: '3d', tool: null });
    cancelCommand(true);
    startDirect(direct, splitOwner);
  },
});

export const splitRect = splitTool('architecture.paintSplitRect', 'Split by rectangle', SquareDashed, 'rect');
export const splitLine = splitTool('architecture.paintSplitLine', 'Split by line', Spline, 'line');

export const finishPainter: ToolDef = { id: 'architecture.finishPainter', label: 'Finish painter', icon: PaintBucket, shortcut: 'FP', run: () => openPainter() };

export const surfaceFinishSchedule: ToolDef = {
  id: 'architecture.surfaceFinishSchedule', label: 'Surface finishes schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'finishZones' }).then((r) => { if (r) notify('Surface finishes schedule made: it is in the project browser, under Schedules.'); }),
};

/** The palette as the engine lists it. */
export const paletteQuery = () => query<PaletteEntry[]>('architecture.finishPalette', {});
