/**
 * The diagram palette as the open project keeps it (decision 885, forum topic 283; docs/plan-diagram-2d.md): the 3D's Diagram
 * display and the plans, sections and elevations the engine draws as diagrams share it. Edited in Preferences → Display →
 * Diagram colours, one undo step a change (`diagram.setPalette`).
 */

import { useMemo } from 'react';
import { execute, useSession } from '../engine/session';
import { effectiveDiagram, palettePatch, projectPaletteOf, resetDiagramPalette, useDiagram, type DiagramCategory, type DiagramSettings } from './diagram';

/** What the 3D draws the diagram with: this computer's variant and the project's palette (else this computer's, kept before). */
export function useDiagramSettings(): DiagramSettings {
  const local = useDiagram();
  const nodes = useSession((s) => s.nodes) as ReadonlyMap<string, { type: string; props: Record<string, unknown> }>;
  // The project's palette as text, so the settings stay the same object until a colour changes (the 3D restyles only then).
  const own = useMemo(() => JSON.stringify(projectPaletteOf(nodes)), [nodes]);
  return useMemo(() => effectiveDiagram(local, JSON.parse(own) as ReturnType<typeof projectPaletteOf>), [local, own]);
}

/** A category's colour changed (null: the default again): the project takes the palette shown with the change, and this computer's palette is let go. */
export function setDiagramColours(change: Partial<Record<DiagramCategory, string | null>>): void {
  const shown = effectiveDiagram(useDiagram.getState(), projectPaletteOf(useSession.getState().nodes as ReadonlyMap<string, { type: string; props: Record<string, unknown> }>)).palette;
  void execute('diagram.setPalette', { palette: palettePatch(shown, change) });
  if (Object.keys(useDiagram.getState().palette).length > 0) resetDiagramPalette();
}

/** The default palette again, in the project and on this computer. */
export function resetDiagramColours(): void {
  void execute('diagram.setPalette', { reset: true });
  resetDiagramPalette();
}
