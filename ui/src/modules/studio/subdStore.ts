/**
 * The SubD cage edit mode's state (docs/plan-studio-freeform.md §8, F1c): which solid, its cage as the engine tells it, what the
 * filter picks and what is picked, the drag in hand. Entered by the card's button, Tab or a double click on the solid; left by
 * Esc, Tab, or another selection. Leaving is not an undo step; each edit is one.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, undo, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { doubleClickHandlers, pickHandlers, tabHandlers } from '../../platform/scope';
import type { Vec3 } from '../../viewport/camera';
import type { Lock3, Snapped3 } from '../../viewport/snap3d';
import { isSubD, moveArgs, none, opArgs, transformArgs, withMode, type Cage, type Op, type Picked, type PickMode, type Transform } from './subdModel';

/**
 * A drag of the picked points: where it began, where the cursor infers it now, the axis held and the distance typed. `held`: the
 * drag outlives the button (Tab, or a digit typed, during it), so a click or Enter ends it, not the release.
 */
export interface SubDDrag { base: Vec3; at: Snapped3 | null; lock: Lock3; text: string; moved: boolean; held?: boolean }

/** A drag dropped by letting go: its translation, where the base went, the text typed since, and the model's version it left. */
export interface SubDAfter { by: Vec3; at: Vec3; text: string; version: number }

export interface SubDEditState {
  /** The SubD solid being edited; null: the mode is off. */
  id: string | null;
  cage: Cage | null;
  mode: PickMode;
  picked: Picked;
  /** The thing under the cursor, lit before a click. */
  hover: { mode: PickMode; key: string } | null;
  drag: SubDDrag | null;
  /** The move just dropped, which a distance typed after the release changes (as in SketchUp); null once anything else is done. */
  after: SubDAfter | null;
  /** The distance the extrude asks, in metres; and the typed scale and turn. */
  extrude: number;
  scale: number;
  turn: number;
}

const idle: SubDEditState = { id: null, cage: null, mode: 'vertex', picked: none('vertex'), hover: null, drag: null, after: null, extrude: 0.3, scale: 1, turn: 15 };
export const useSubD = create<SubDEditState>(() => ({ ...idle }));

/** The engine's refusal (a symmetry a face crosses, a crease on nothing…), shown. */
const refused = (): void => { const e = useSession.getState().error; if (e) notify(e); };

/** The cage of the solid being edited, asked again. A reply that comes after another solid was entered is dropped. */
export async function refreshCage(): Promise<Cage | null> {
  const id = useSubD.getState().id;
  if (!id) return null;
  const cage = await query<Cage>('studio.subdCage', { id });
  if (useSubD.getState().id !== id) return null;
  if (!cage) { leaveCage(); return null; }
  useSubD.setState({ cage });
  return cage;
}

/** Enters the mode on a SubD solid (the one selected by default), in the 3D view. */
export function enterCage(id?: string | null): boolean {
  const target = id ?? useUi.getState().selection;
  const node = target ? useSession.getState().nodes.get(target) : undefined;
  if (!isSubD(node)) { notify('Select one SubD solid first.'); return false; }
  useUi.setState({ view: '3d', selection: node!.id, selected: [node!.id] });
  useSubD.setState({ ...idle, id: node!.id });
  void refreshCage();
  return true;
}

export function leaveCage(): void { useSubD.setState({ ...idle }); }
export const editingCage = (): string | null => useSubD.getState().id;

/** The filter changed: what was picked of another kind is let go. */
export function setMode(mode: PickMode): void {
  useSubD.setState((s) => ({ mode, picked: withMode(s.picked, mode), hover: null, after: null }));
}

/** A drag let go: ONE subdEdit with the translation. Returns whether the engine took it. */
export async function applyMove(by: Vec3): Promise<boolean> {
  const { id, picked } = useSubD.getState();
  const args = id ? moveArgs(id, picked, by) : null;
  if (!args) return false;
  const r = await execute('studio.subdEdit', args);
  if (r === null) refused();
  await refreshCage();
  return r !== null;
}

/**
 * The move just dropped, made `to` instead (a distance typed after the release): the drop is undone and `to` applied, so the
 * two stay ONE undo step; when the model changed in between, only the difference is applied.
 */
export async function redoMoveAs(to: Vec3): Promise<boolean> {
  const a = useSubD.getState().after;
  if (!a) return false;
  useSubD.setState({ after: null });
  if (useSession.getState().version === a.version) { await undo(); return applyMove(to); }
  return applyMove({ x: to.x - a.by.x, y: to.y - a.by.y, z: to.z - a.by.z });
}

/** A typed scale and turn about the picked middle: ONE subdEdit. */
export async function applyTransform(t: Transform): Promise<boolean> {
  useSubD.setState({ after: null });
  const { id, picked } = useSubD.getState();
  const args = id ? transformArgs(id, picked, t) : null;
  if (!args) return false;
  const r = await execute('studio.subdEdit', args);
  if (r === null) refused();
  await refreshCage();
  return r !== null;
}

/** A shaping op on what is picked: ONE subdOp. The points and faces are numbered anew, so the pick is let go and the cage asked again. */
export async function applyOp(op: Op): Promise<boolean> {
  useSubD.setState({ after: null });
  const { id, picked, mode } = useSubD.getState();
  const args = id ? opArgs(id, picked, op) : null;
  if (!args) return false;
  const r = await execute('studio.subdOp', args);
  if (r === null) refused();
  else useSubD.setState({ picked: none(mode), hover: null, drag: null });
  await refreshCage();
  return r !== null;
}

/** A setting of the solid (levels, thickness, side, symmetry): ONE subdSet. The engine's refusal is shown. Returns whether it was taken. */
export async function applySet(id: string, args: Record<string, unknown>): Promise<boolean> {
  const r = await execute('studio.subdSet', { id, ...args });
  if (r === null) refused();
  if (useSubD.getState().id === id) await refreshCage();
  return r !== null;
}

/** Tab and a double click on a SubD solid enter the mode (Tab, or Esc, leave it); taken before the part edit mode's. Registered once. */
export function registerSubDEditMode(): void {
  const inField = (): boolean => !!(document.activeElement as HTMLElement | null)?.closest?.('input, select, textarea, [contenteditable="true"], [role="dialog"]');
  tabHandlers.add(() => {
    const ui = useUi.getState();
    if (inField() || ui.dialog) return false;
    if (editingCage()) { leaveCage(); return true; }
    if (ui.tool || ui.view !== '3d' || ui.selected.length !== 1) return false;
    return isSubD(ui.selection ? useSession.getState().nodes.get(ui.selection) : null) ? enterCage(ui.selection) : false;
  });
  // A plain click in the mode picks nothing else: on the solid or on empty space it lets go of what is picked.
  pickHandlers.add(() => {
    if (!editingCage()) return false;
    useSubD.setState((s) => ({ picked: none(s.mode) }));
    return true;
  });
  doubleClickHandlers.add((id) => {
    if (!id || useUi.getState().tool) return false;
    return isSubD(useSession.getState().nodes.get(id) as NodeDto | undefined) ? enterCage(id) : false;
  });
}
