/** The modules of the product this build was made for (`virtual:lapin-modules`, from products/<id>.json). */

import loaded, { product } from 'virtual:lapin-modules';
import type { ComponentType } from 'react';
import type { Grip, GripView, LapinModule, RefLine, RibbonTabDef, ToolDef } from './module';
import type { NodeDto } from '../engine/protocol';
import { toggleTool, useUi } from '../app/store';
import { mergeTabs } from './ribbon';
import { cancel, useCad } from '../viewport/cad/session';

export const modules: readonly LapinModule[] = loaded;
export const productInfo = product;

export const ribbonTabs = (): RibbonTabDef[] => mergeTabs(modules, product.tabs ?? undefined);
export const hotbarTools = (): ToolDef[] => modules.flatMap((m) => m.hotbar ?? []).slice(0, 9);
export const propertySections = (ifcClass: string) => modules.flatMap((m) => m.propertySections ?? []).filter((s) => s.classes.includes(ifcClass) || s.classes.includes('*'));
export const backgrounds = (): ComponentType[] => modules.map((m) => m.background).filter((b): b is ComponentType => Boolean(b));
export const dialog = (id: string): ComponentType | null => modules.map((m) => m.dialogs?.[id]).find(Boolean) ?? null;

export const gripsOf = (node: NodeDto, view?: GripView): Grip[] => modules.flatMap((m) => m.grips?.[node.type]?.(node, view) ?? []).map((g) => ({ owner: node.id, ...g }));
export const referencesOf = (node: NodeDto): RefLine[] => modules.flatMap((m) => m.references?.[node.type]?.(node) ?? []);
export const similarOf = (cls: string) => modules.map((m) => m.similar?.[cls]).find(Boolean) ?? null;
export const setTypeOf = (cls: string) => modules.map((m) => m.setType?.[cls]).find(Boolean) ?? null;
export const typeFieldsOf = (cls: string) => modules.map((m) => m.typeFields?.[cls]).find(Boolean) ?? null;

/** Every tool of every module, once. */
export const allTools = (): ToolDef[] => {
  const seen = new Map<string, ToolDef>();
  for (const m of modules) {
    for (const t of m.hotbar ?? []) seen.set(t.id, t);
    for (const tab of m.tabs ?? []) for (const g of tab.groups) for (const t of [...g.tools, ...(g.small ?? [])]) seen.set(t.id, t);
    for (const g of Object.values(m.sharedGroups ?? {})) for (const t of [...g.tools, ...(g.small ?? [])]) seen.set(t.id, t);
  }
  return [...seen.values()];
};

export const findTool = (id: string | null): ToolDef | null => (id ? allTools().find((t) => t.id === id) ?? null : null);

/** Press a tool: a drawing tool switches on (or off), a button does its thing. */
export function activate(t: ToolDef): void {
  // A tool ends the drafting command that was running.
  if (useCad.getState().step) cancel(true);
  if (t.gesture) toggleTool(t.id);
  else if (t.run) { useUi.setState({ tool: null }); t.run(); }
}
