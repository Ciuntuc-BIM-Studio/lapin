/**
 * Columns' and beams' temporary dimensions in plan (decision 733), the measuring shared with walls (viewport/lineDims.ts).
 * A column measures from its two lines (along its own x and y, through its insertion point) across to the nearest parallel
 * wall, column or grid axis each side; a beam across to parallel beams, walls and axes either side, and from its free ends
 * along. What is here: the lines of a column about to be placed (the cursor's point, its type's section and finish layers,
 * as the engine's `view.dimLines` would give it once placed), a beam's being drawn, and the move a typed value makes. Pure.
 */

import type { Vec2 } from '../../platform/module';
import { acrossShift, frameOf, lineDims, type AxisLine, type DimLineInfo, type DimPick, type DimRef, type Host, type LineDim } from '../../viewport/lineDims';

/** What a column type's props say of its section and finish layers, as far as its extents go. */
export interface ColumnTypeProps {
  section?: { kind?: string; width?: number; depth?: number; rings?: [number, number][][] };
  finishLayers?: { thickness?: number; faces?: string[] }[];
}

/** The section's extents in its own axes (x along its width): its outline's box, centred unless drawn as rings. */
function extents(s: ColumnTypeProps['section']): { x0: number; x1: number; y0: number; y1: number } {
  const rings = s?.rings?.filter((r) => r.length >= 3);
  if (rings?.length) {
    const pts = rings.flat();
    return { x0: Math.min(...pts.map((p) => p[0])), x1: Math.max(...pts.map((p) => p[0])), y0: Math.min(...pts.map((p) => p[1])), y1: Math.max(...pts.map((p) => p[1])) };
  }
  const w = (s?.width ?? 0.3) / 2, d = (s?.depth ?? s?.width ?? 0.3) / 2;
  return { x0: -w, x1: w, y0: -d, y1: d };
}

/**
 * A column's two lines at `at`, turned `rotation` degrees (x then y, as the engine's MemberDimLines): its core the section, its
 * faces the finish layers' outer outline, each face pushed out by the layers covering it (all four when a layer names none).
 */
export function columnLines(id: string, name: string, at: Vec2, type: ColumnTypeProps | null | undefined, rotation = 0): DimLineInfo[] {
  const c = extents(type?.section);
  const push = { '+x': 0, '-x': 0, '+y': 0, '-y': 0 } as Record<string, number>;
  for (const l of type?.finishLayers ?? []) {
    if (!(typeof l.thickness === 'number' && l.thickness > 0)) continue;
    const faces = Array.isArray(l.faces) && l.faces.length ? l.faces : ['+x', '-x', '+y', '-y'];
    for (const f of faces) if (f in push) push[f] += l.thickness;
  }
  const f = { x0: c.x0 - push['-x'], x1: c.x1 + push['+x'], y0: c.y0 - push['-y'], y1: c.y1 + push['+y'] };
  const r = (rotation * Math.PI) / 180, cs = Math.cos(r), sn = Math.sin(r);
  const P = (x: number, y: number): Vec2 => ({ x: at.x + x * cs - y * sn, y: at.y + x * sn + y * cs });
  return [
    { id, name, kind: 'column', part: 'x', a: P(f.x0, 0), b: P(f.x1, 0), faces: [f.y0, f.y1], core: [c.y0, c.y1] },
    { id, name, kind: 'column', part: 'y', a: P(0, f.y0), b: P(0, f.y1), faces: [-f.x1, -f.x0], core: [-c.x1, -c.x0] },
  ];
}

/** A column's dimensions from both its lines (keys `x:…` and `y:…`), to walls, columns and grid axes. */
export function columnDims(own: DimLineInfo[], lines: DimLineInfo[], axes: AxisLine[], picks: Record<string, DimPick>, mode: DimRef): { host: Host; dims: LineDim[] }[] {
  const targets = lines.filter((l) => l.kind === 'wall' || l.kind === 'column');
  return own.map((l) => {
    const host: Host = { id: l.id, a: l.a, b: l.b, ref: 0, faces: l.faces, core: l.core };
    return { host, dims: lineDims(host, targets, axes, picks, { mode, along: false, prefix: `${l.part ?? 'x'}:` }) };
  });
}

/** A beam's dimensions: across to beams, walls and grid axes, along from its free ends to the beam or wall it runs toward. */
export function beamDims(host: Host, lines: DimLineInfo[], axes: AxisLine[], picks: Record<string, DimPick>, mode: DimRef): LineDim[] {
  return lineDims(host, lines.filter((l) => l.kind === 'beam' || l.kind === 'wall'), axes, picks, { mode });
}

/** How far a column moves (plan metres) so that an across dimension of one of its lines reads `value`; null: it already does. */
export function columnMove(host: Host, d: LineDim, value: number): Vec2 | null {
  const by = acrossShift(d, value);
  if (by == null) return null;
  const { n } = frameOf(host);
  return { x: n.x * by, y: n.y * by };
}
