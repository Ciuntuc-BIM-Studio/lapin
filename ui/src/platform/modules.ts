/** The modules of the product this build was made for (`virtual:lapin-modules`, from products/<id>.json). */

import loaded, { product } from 'virtual:lapin-modules';
import type { ComponentType } from 'react';
import type { LapinModule, RibbonTabDef, ToolDef } from './module';
import { toggleTool, useUi } from '../app/store';

export const modules: readonly LapinModule[] = loaded;
export const productInfo: { id: string; name: string } = product;

export const ribbonTabs = (): RibbonTabDef[] => modules.flatMap((m) => m.tabs ?? []).sort((a, b) => a.order - b.order);
export const hotbarTools = (): ToolDef[] => modules.flatMap((m) => m.hotbar ?? []).slice(0, 9);
export const propertySections = (ifcClass: string) => modules.flatMap((m) => m.propertySections ?? []).filter((s) => s.classes.includes(ifcClass));
export const dialog = (id: string): ComponentType | null => modules.map((m) => m.dialogs?.[id]).find(Boolean) ?? null;

/** Every tool of every module, once. */
export const allTools = (): ToolDef[] => {
  const seen = new Map<string, ToolDef>();
  for (const m of modules) {
    for (const t of m.hotbar ?? []) seen.set(t.id, t);
    for (const tab of m.tabs ?? []) for (const g of tab.groups) for (const t of [...g.tools, ...(g.small ?? [])]) seen.set(t.id, t);
  }
  return [...seen.values()];
};

export const findTool = (id: string | null): ToolDef | null => (id ? allTools().find((t) => t.id === id) ?? null : null);

/** Press a tool: a drawing tool switches on (or off), a button does its thing. */
export function activate(t: ToolDef): void {
  if (t.gesture) toggleTool(t.id);
  else if (t.run) { useUi.setState({ tool: null }); t.run(); }
}
