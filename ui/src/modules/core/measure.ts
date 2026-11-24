/**
 * Measure (ME, the second quick tool; a tester's report, 03.11.2026: the button was there and did nothing): click points in a
 * plan, a section, an elevation or the 3D view, Enter to finish. Two modes (G2 of docs/plan-gaussian-splats.md): **Distance**,
 * a chain of legs, its running total shown while it is drawn and each leg, the total (and, from three points on, the plan area)
 * said at the end; **Area**, a closed outline, its area and perimeter in the plane that best fits its points (a facade's
 * patch on a scan, a roof's slope), said live and at the end. In the 3D view its points stay where they were snapped (a
 * point cloud's points, a splat's centre). It draws nothing that stays.
 */

import { createElement } from 'react';
import { create } from 'zustand';
import { Ruler } from 'lucide-react';
import type { Gesture, ToolDef, Vec2 } from '../../platform/module';
import { notify } from '../../app/store';
import { lengthText } from '../../engine/unitsStore';
import { useToolTick } from '../../platform/modules';

type V3 = { x: number; y: number; z: number };

/** The legs' lengths, their total, and the area the points enclose (from three on; m²). */
export function measured(points: Vec2[]): { legs: number[]; total: number; area: number | null } {
  const legs = points.slice(1).map((p, i) => Math.hypot(p.x - points[i].x, p.y - points[i].y));
  let a = 0;
  for (let i = 0; i < points.length; i++) {
    const p = points[i], q = points[(i + 1) % points.length];
    a += p.x * q.y - q.x * p.y;
  }
  return { legs, total: legs.reduce((s, l) => s + l, 0), area: points.length >= 3 ? Math.abs(a) / 2 : null };
}

/**
 * The same in space, for points clicked in the 3D view with their heights: each leg's true length (a ridge to an eave is
 * longer than its plan), the total, and the plan area they enclose.
 */
export function measured3d(points: Vec2[], heights: number[]): { legs: number[]; total: number; area: number | null; rise: number } {
  const flat = measured(points);
  const legs = points.slice(1).map((p, i) => Math.hypot(p.x - points[i].x, p.y - points[i].y, (heights[i + 1] ?? 0) - (heights[i] ?? 0)));
  return { legs, total: legs.reduce((s, l) => s + l, 0), area: flat.area, rise: (heights[heights.length - 1] ?? 0) - (heights[0] ?? 0) };
}

/**
 * A closed outline in space: its area in the plane that best fits it (Newell's normal: exact for a flat outline, the area
 * seen square to that plane for one that is not quite), its perimeter (the closing leg counted), and that plane's tilt
 * from level (0° a floor, 90° a wall).
 */
export function outlineArea(points: readonly V3[]): { area: number; perimeter: number; tilt: number } {
  let nx = 0, ny = 0, nz = 0, perimeter = 0;
  for (let i = 0; i < points.length; i++) {
    const p = points[i], q = points[(i + 1) % points.length];
    nx += (p.y - q.y) * (p.z + q.z);
    ny += (p.z - q.z) * (p.x + q.x);
    nz += (p.x - q.x) * (p.y + q.y);
    if (points.length > 1) perimeter += Math.hypot(q.x - p.x, q.y - p.y, q.z - p.z);
  }
  const n = Math.hypot(nx, ny, nz);
  const tilt = n > 0 ? (Math.acos(Math.min(1, Math.abs(nz) / n)) * 180) / Math.PI : 0;
  return { area: points.length >= 3 ? n / 2 : 0, perimeter: points.length === 2 ? perimeter / 2 : perimeter, tilt };
}

export type MeasureMode = 'distance' | 'area';
const useMeasureMode = create<{ mode: MeasureMode }>(() => ({ mode: 'distance' }));
export const measureMode = (): MeasureMode => useMeasureMode.getState().mode;
export function setMeasureMode(mode: MeasureMode): void {
  useMeasureMode.setState({ mode });
  // The views read the gesture as they draw: a gesture half made starts over (`mode`).
  useToolTick.setState((s) => ({ n: s.n + 1 }));
}

function ModeOption() {
  const mode = useMeasureMode((s) => s.mode);
  return createElement('select', {
    className: `cad-layer${mode === 'area' ? ' on' : ''}`, value: mode, 'data-testid': 'measure-mode',
    title: 'A chain of distances with their running total, or a closed outline: its area and perimeter in the plane that best fits it',
    onChange: (e: { target: { value: string } }) => setMeasureMode(e.target.value as MeasureMode),
  }, createElement('option', { value: 'distance' }, 'Distance'), createElement('option', { value: 'area' }, 'Area'));
}

const DISTANCE: Gesture = { kind: 'polyline' };
const AREA: Gesture = { kind: 'polygon' };

/** What the chip says while the points are picked: the running total, or the outline's area and perimeter. */
export function measureInfo(points: readonly V3[], mode: MeasureMode): string | null {
  if (points.length < 2) return null;
  if (mode === 'area') {
    const o = outlineArea(points);
    return points.length >= 3 ? `Area ${o.area.toFixed(2)} m² · perimeter ${lengthText(o.perimeter)}` : null;
  }
  let total = 0;
  for (let i = 1; i < points.length; i++) total += Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y, points[i].z - points[i - 1].z);
  return `Total ${lengthText(total)}`;
}

export const measure: ToolDef = {
  id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME',
  get gesture(): Gesture { return measureMode() === 'area' ? AREA : DISTANCE; },
  mode: () => measureMode(),
  options: ModeOption,
  inSpace: true,
  liveInfo: (points) => measureInfo(points, measureMode()),
  complete: ({ points, heights }) => {
    const inSpace = !!heights && heights.length === points.length;
    if (measureMode() === 'area') {
      if (points.length < 3) return;
      const o = outlineArea(points.map((p, i) => ({ x: p.x, y: p.y, z: inSpace ? heights![i] : 0 })));
      const parts = [`Area ${o.area.toFixed(2)} m²`, `perimeter ${lengthText(o.perimeter)}`];
      if (inSpace && o.tilt > 0.5) parts.push(`its plane ${o.tilt.toFixed(1)}° from level`);
      notify(parts.join(' · '));
      return;
    }
    if (points.length < 2) return;
    // Clicked in the 3D view on points at different heights: the distance in space.
    if (inSpace && heights!.some((h) => Math.abs(h - heights![0]) > 1e-3)) {
      const m = measured3d(points, heights!);
      const parts = [`Length ${lengthText(m.total)}`];
      if (m.legs.length > 1) parts.push(`legs ${m.legs.map((l) => lengthText(l)).join(' + ')}`);
      else {
        const [a, b] = points;
        parts.push(`Δx ${lengthText(Math.abs(b.x - a.x))}`, `Δy ${lengthText(Math.abs(b.y - a.y))}`, `Δz ${lengthText(Math.abs(m.rise))}`);
      }
      notify(parts.join(' · '));
      return;
    }
    const { legs, total, area } = measured(points);
    const parts = [`Length ${lengthText(total)}`];
    if (legs.length > 1) parts.push(`legs ${legs.map((l) => lengthText(l)).join(' + ')}`);
    else {
      const [a, b] = points;
      parts.push(`Δx ${lengthText(Math.abs(b.x - a.x))}`, `Δy ${lengthText(Math.abs(b.y - a.y))}`, `angle ${(Math.atan2(b.y - a.y, b.x - a.x) * 180 / Math.PI).toFixed(1)}°`);
    }
    if (area != null && area > 1e-9) parts.push(`area ${area.toFixed(2)} m²`);
    notify(parts.join(' · '));
  },
};
