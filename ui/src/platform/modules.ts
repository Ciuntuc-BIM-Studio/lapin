/** The modules of the product this build was made for (`virtual:lapin-modules`, from products/<id>.json). */

import loaded, { product } from 'virtual:lapin-modules';
import type { LapinModule, RibbonTabDef, ToolDef } from './module';

export const modules: readonly LapinModule[] = loaded;
export const productInfo: { id: string; name: string } = product;

export const ribbonTabs = (): RibbonTabDef[] => modules.flatMap((m) => m.tabs ?? []).sort((a, b) => a.order - b.order);
export const hotbarTools = (): ToolDef[] => modules.flatMap((m) => m.hotbar ?? []).slice(0, 9);
export const propertySections = (kind: string) => modules.flatMap((m) => m.propertySections ?? []).filter((s) => s.kinds.includes(kind));
