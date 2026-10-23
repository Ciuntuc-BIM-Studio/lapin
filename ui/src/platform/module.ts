/**
 * What a Lapin module brings to the interface: its ribbon tabs, the quick
 * tools of the hotbar, panels and property sections. A product
 * (products/<id>.json) lists its modules, and a build bundles only those
 * (`virtual:lapin-modules`), so a client gets exactly the modules it has.
 * The engine side of a module (C#) is loaded the same way, by id.
 */

import type { ComponentType } from 'react';
import type { LucideIcon } from 'lucide-react';

export interface ToolDef {
  id: string;
  label: string;
  icon: LucideIcon;
  /** Two-letter shortcut, as in Revit (WA, DR…). */
  shortcut?: string;
  /** Runs the tool; absent: a mock button for now. */
  run?: () => void;
}

export interface RibbonGroupDef { label: string; tools: ToolDef[]; small?: ToolDef[] }

export interface RibbonTabDef {
  id: string;
  label: string;
  groups: RibbonGroupDef[];
  /** Lower comes first (Architecture before Annotate). */
  order: number;
}

/** A section of the properties panel for a selected element of a kind. */
export interface PropertySectionDef { id: string; label: string; kinds: string[]; component: ComponentType<{ kind: string }> }

export interface LapinModule {
  id: string;
  name: string;
  /** One line for the about / product pages. */
  summary: string;
  tabs?: RibbonTabDef[];
  /** Tools for the viewport hotbar (1–9), in order. */
  hotbar?: ToolDef[];
  propertySections?: PropertySectionDef[];
}
