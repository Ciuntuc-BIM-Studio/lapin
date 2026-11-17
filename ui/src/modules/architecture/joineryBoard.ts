/**
 * A window type's outline of its own on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): the grid form
 * stays the default way; Draw outline… opens the type's elevation as a board (`architecture.editJoineryOutline`), its outline as
 * one closed polyline, the frame's inner line dashed. Finish writes the drawn outline into the type (`architecture.finishJoineryOutline`:
 * its width and height the outline's, a frame following it and one fixed light), one undo step; Cancel drops the board.
 */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { hasModule } from '../../platform/brand';
import { registerBoard, SKETCH_TOOLS, type Board } from '../../viewport/sketch/board';

const back = new Map<string, { view: ReturnType<typeof useUi.getState>['view']; drawingViewId: string | null }>();

/** Whether a type's outline can be drawn on a board in this edition (drafting draws on it, Studio's bar finishes it). */
export const outlineBoardAvailable = (): boolean => hasModule('drafting') && hasModule('studio');

async function run<T>(command: string, args: Record<string, unknown>): Promise<T | null> {
  const r = await execute(command, args) as T | null;
  const err = useSession.getState().error;
  if (r == null && err) notify(err);
  return r;
}

/** Opens a saved window type's outline on its board. */
export async function editJoineryOutline(typeId: string): Promise<void> {
  const r = await run<{ viewId?: string }>('architecture.editJoineryOutline', { typeId });
  if (!r?.viewId) return;
  const s = useUi.getState();
  if (!back.has(r.viewId)) back.set(r.viewId, { view: s.view, drawingViewId: s.drawingViewId });
  useUi.setState({ dialog: null, view: 'drawing', drawingViewId: r.viewId, tool: null, selected: [], selection: null });
  notify('Draw one closed outline (seen from outside, its bottom on the sill line), then Finish sketch.');
}

function leave(viewId: string) {
  const b = back.get(viewId);
  back.delete(viewId);
  useUi.setState({ view: b && b.view !== 'drawing' ? b.view : b?.drawingViewId ? 'drawing' : 'plan', drawingViewId: b?.drawingViewId ?? null, dialog: 'joinery', selected: [], selection: null });
}

function joineryBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const v = nodes.get(viewId);
  const of = v?.props.joineryOutlineOf as { typeId?: string } | undefined;
  if (!v || !of || typeof of !== 'object') return null;
  return {
    kind: 'joinery', viewId, title: nodes.get(of.typeId ?? '')?.name ?? v.name, hint: 'One closed outline: a polyline, a circle, or lines and arcs end to end', tools: SKETCH_TOOLS, constraints: false,
    finish: async () => { if (await run('architecture.finishJoineryOutline', { viewId })) leave(viewId); },
    cancel: async () => { await run('architecture.cancelJoineryOutline', { viewId }); leave(viewId); },
  };
}

registerBoard(joineryBoard);
