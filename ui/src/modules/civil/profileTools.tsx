/**
 * The profile editor's tools (site grading T3): Profile on a line (pick a line in the plan: the panel opens on it) and Section
 * line (a polyline drawn over the terrain: a feature line that reads the ground, and edits it within an influence width).
 */

import { ChartSpline, Scissors } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import { notify } from '../../app/store';
import { toggleOutlineArcs } from '../../viewport/outlineShape';
import { isDitch } from './prefs';
import { openProfile } from './profileStore';

/** The lines that have a profile: a feature line, a hardscape, a path or road, a retaining wall, a ditch. */
const PROFILED = ['LapinFeatureLine', 'LapinHardscape', 'IfcPavement', 'LapinRetainingWall', 'IfcEarthworksCut'];

export const profileOnLine: ToolDef = {
  id: 'civil.profileOnLine', label: 'Profile on a line', icon: ChartSpline, shortcut: 'LP',
  tooltip: 'Pick a line in the plan (a feature line, a hardscape, a path, a wall or a ditch): its profile opens as a graph to edit',
  gesture: { kind: 'pick', types: PROFILED, reach: 12 },
  complete: ({ entity }) => {
    if (!entity) return;
    if (entity.type === 'IfcEarthworksCut' && !isDitch(entity)) { notify('A platform has no profile: pick a ditch, or draw a section line across it.'); return; }
    openProfile(entity.id);
  },
};

/** The rises of the edges drawn: one per edge (n − 1). */
const rises = (n: number, arcs?: number[]): number[] => Array.from({ length: n - 1 }, (_, i) => arcs?.[i] ?? 0);

export const sectionLine: ToolDef = {
  id: 'civil.sectionLine', label: 'Section line', icon: Scissors, shortcut: 'SQ',
  tooltip: 'Draw a line over the terrain: its profile opens, and editing it reshapes the ground within an influence width each side',
  gesture: { kind: 'polyline', arcs: true },
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 2) return;
    const r = rises(points.length, arcs);
    const made = await ctx.execute('civil.addSectionLine', { points: points.map((p) => [p.x, p.y]), ...(r.some((v) => Math.abs(v) > 1e-6) ? { arcs: r } : {}) }) as { id?: string } | null;
    if (made?.id) openProfile(made.id);
  },
};
