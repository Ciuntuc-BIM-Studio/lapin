/** The modules of the product this build was made for (`virtual:lapin-modules`, from products/<id>.json). */

import { create } from 'zustand';
import loaded, { product } from 'virtual:lapin-modules';
import type { ComponentType } from 'react';
import type * as THREE from 'three';
import type { PlacementRequest, Vec2, Grip, GripView, LapinModule, ModuleViewDef, PlanOverlayProps, DrawingOverlayProps, RefLine, RibbonTabDef, ToolDef } from './module';
import type { NodeDto } from '../engine/protocol';
import { addModuleViews, toggleTool, useUi } from '../app/store';
import { mergeTabs } from './ribbon';
import { cancel, useCad } from '../viewport/cad/session';
import { parseSlot, setSlots, slotsOf, useHotbars, withSlot, withSlotType, withSwap, type Slots } from '../app/hotbar';
import { useSession } from '../engine/session';
import { useInterface } from '../app/interface';
import { addSelectionExpander } from './groups';
import { snapGrip } from '../viewport/grips';
import { askPlacement as ask } from './placement';
import { replacementsOf, replacesAny, through, withReplacedTools, type Replacement } from './replaces';
import { extraTools } from './extraTools';

/** The product's modules, with the tools some of them replace in place (docs/overrides.md); as loaded when none does. */
export const modules: readonly LapinModule[] = withReplacedTools(loaded);
export const productInfo = product;
/** What the modules replace of each other's and the shell's (Help → About lists it): none in this repository's editions. */
export const replacements: readonly Replacement[] = replacementsOf(loaded);
for (const m of modules) if (m.expandSelection) addSelectionExpander(m.expandSelection);
addModuleViews([...modules.flatMap((m) => Object.keys(m.views ?? {})), ...replacements.filter((r) => r.kind === 'view').map((r) => r.id)]);

/** A replaced dialog or view, made once (a component made anew on every render would be mounted anew). */
const resolved = new Map<string, unknown>();
function replacedOnce<T>(kind: 'dialogs' | 'views', id: string, original: () => T): T {
  if (!replacesAny(loaded, kind, id)) return original();
  const key = `${kind}:${id}`;
  if (!resolved.has(key)) resolved.set(key, through(loaded, kind, id, original()));
  return resolved.get(key) as T;
}
/** What the modules show over the active model view (the planning module's card to drag the selection onto a task). */
export const overlays = (): ComponentType[] => modules.flatMap((m) => m.overlays ?? []);

/** What the modules draw over the 3D view's canvas, projected by its camera. */
export const overlays3d = (): ComponentType<{ camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }>[] => modules.flatMap((m) => m.overlays3d ?? []);

/** What the modules draw inside the plan's SVG, in the plan's own coordinates. */
export const planOverlays = (): ComponentType<PlanOverlayProps>[] => modules.flatMap((m) => m.planOverlays ?? []);
export const drawingOverlays = (): ComponentType<DrawingOverlayProps>[] => modules.flatMap((m) => m.drawingOverlays ?? []);
/** The grip's drop point as the modules' `gripSnap`s keep it (the point itself when none answers). */
export const gripSnapped = (grip: Grip, to: Vec2, free: boolean): Vec2 => {
  const node = grip.owner ? useSession.getState().nodes.get(grip.owner) : undefined;
  return node ? snapGrip(modules.map((m) => m.gripSnap), node, grip, to, free) : to;
};

/** Asks the modules' `placementCheck`s whether a door or window fits there; the answer comes to `usePlacement` (platform/placement.ts). */
export const askPlacement = (req: PlacementRequest): void => ask(req, modules.flatMap((m) => (m.placementCheck ? [m.placementCheck] : [])));

export const ribbonTabs = (): RibbonTabDef[] => mergeTabs(modules, product.tabs ?? undefined);
/** The modules' own quick tools, in their order: a workspace's slots until the user sets them. */
export const defaultHotbar = (): ToolDef[] => modules.flatMap((m) => m.hotbar ?? []);
export const propertySections = (ifcClass: string) => modules.flatMap((m) => m.propertySections ?? []).filter((s) => s.classes.includes(ifcClass) || s.classes.includes('*'));
export const backgrounds = (): ComponentType[] => modules.map((m) => m.background).filter((b): b is ComponentType => Boolean(b));
/** The dialog the store's `dialog` names: a module's, else the shell's (`shell`), as a module may replace it; null when none. */
export const dialog = (id: string, shell?: Record<string, ComponentType>): ComponentType | null =>
  replacedOnce<ComponentType | null>('dialogs', id, () => modules.map((m) => m.dialogs?.[id]).find(Boolean) ?? shell?.[id] ?? null);
/** A kind of view a module adds (the Gantt), as a module may replace it; null when no module of the product has it. */
export const moduleView = (kind: string): ModuleViewDef | null =>
  replacedOnce<ModuleViewDef | null>('views', kind, () => modules.map((m) => m.views?.[kind]).find(Boolean) ?? null);

// A pinned element shows no grips: it stays where it is (decision 449).
export const gripsOf = (node: NodeDto, view?: GripView): Grip[] => node.props.pinned === true ? [] : modules.flatMap((m) => m.grips?.[node.type]?.(node, view) ?? []).map((g) => ({ owner: node.id, ...g }));
export const referencesOf = (node: NodeDto): RefLine[] => modules.flatMap((m) => m.references?.[node.type]?.(node) ?? []);
export const similarOf = (cls: string) => modules.map((m) => m.similar?.[cls]).find(Boolean) ?? null;
export const setTypeOf = (cls: string) => modules.map((m) => m.setType?.[cls]).find(Boolean) ?? null;
export const typeFieldsOf = (cls: string) => modules.map((m) => m.typeFields?.[cls]).find(Boolean) ?? null;

/** Every tool of every module, once; then the tools added while the app runs (a script's: platform/extraTools.ts). */
export const allTools = (): ToolDef[] => {
  const seen = new Map<string, ToolDef>();
  for (const m of modules) {
    for (const t of m.hotbar ?? []) seen.set(t.id, t);
    for (const tab of m.tabs ?? []) for (const g of tab.groups) for (const t of [...g.tools, ...(g.small ?? [])]) seen.set(t.id, t);
    for (const g of Object.values(m.sharedGroups ?? {})) for (const t of [...g.tools, ...(g.small ?? [])]) seen.set(t.id, t);
  }
  for (const t of extraTools()) if (!seen.has(t.id)) seen.set(t.id, t);
  return [...seen.values()];
};

export const findTool = (id: string | null): ToolDef | null => (id ? allTools().find((t) => t.id === id) ?? null : null);

/** The current workspace's quick tool ids, slot by slot (decision 259). */
export const hotbarIds = (hotbars = useHotbars.getState(), workspace = useInterface.getState().workspace): Slots =>
  slotsOf(hotbars, workspace, defaultHotbar().map((t) => t.id), (id) => findTool(id) !== null);

/** A quick slot as it stands: its tool, and its type by name with the project's type of that name (null when the project has none). */
export interface QuickSlot { tool: ToolDef; typeName: string | null; typeId: string | null }

/** The types a tool puts down in the open project, in their order. */
export const typesOf = (t: ToolDef | null): { id: string; name: string }[] => {
  if (!t?.typed) return [];
  const cls = t.typed.cls;
  return [...useSession.getState().nodes.values()].filter((n) => n.type === cls).map((n) => ({ id: n.id, name: n.name ?? '' })).filter((x) => x.name);
};

/** A slot read: its tool, its type's name and the project's type of that name. */
export function quickSlot(slot: string | null): QuickSlot | null {
  const p = parseSlot(slot);
  const tool = p ? findTool(p.tool) : null;
  if (!p || !tool) return null;
  const typeName = tool.typed ? p.type : null;
  return { tool, typeName, typeId: typeName ? typesOf(tool).find((x) => x.name === typeName)?.id ?? null : null };
}

/** The current workspace's quick tools, slot by slot; null for an empty slot. */
export const hotbarSlots = (): (QuickSlot | null)[] => hotbarIds().map(quickSlot);

/** Press a quick slot: its type (when the project has it) made the tool's current one, then the tool. */
export function activateSlot(slot: QuickSlot): void {
  if (slot.typeId) slot.tool.typed?.set(slot.typeId);
  activate(slot.tool);
}

const current = () => useInterface.getState().workspace;
/** A tool put in a quick slot of the current workspace (null empties it). */
export const putInSlot = (i: number, id: string | null) => setSlots(current(), withSlot(hotbarIds(), i, id));
export const swapSlots = (i: number, j: number) => setSlots(current(), withSwap(hotbarIds(), i, j));
/** A quick slot's tool given one of its types by name (null: the tool's current type). */
export const typeSlot = (i: number, type: string | null) => setSlots(current(), withSlotType(hotbarIds(), i, type));
/** The current workspace's quick tools back to the modules' own. */
export const resetSlots = () => setSlots(current(), null);

/** Press a tool: a drawing tool switches on (or off), a button does its thing. */
/** Counts what changed a tool's ghost without the cursor moving (Space): the views redraw it. */
export const useToolTick = create<{ n: number }>(() => ({ n: 0 }));

export function activate(t: ToolDef): void {
  // A tool ends the drafting command that was running.
  if (useCad.getState().step) cancel(true);
  if (t.gesture) toggleTool(t.id);
  else if (t.run) { useUi.setState({ tool: null }); t.run(); }
}
