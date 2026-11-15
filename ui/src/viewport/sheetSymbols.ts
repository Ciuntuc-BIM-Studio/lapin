/**
 * Symbols placed on a sheet (docs/plan-user-extensibility.md §3) are picked, moved and deleted there like the views are
 * (forum topic 106): each one's box on the paper comes from its symbol's preview (its box at scale 1 round the insertion
 * point), turned and flipped as it stands; a click takes the smallest box under the point.
 */

import type { NodeDto } from '../engine/protocol';

export interface SheetSymbol {
  id: string;
  /** The insertion point, paper mm. */
  at: [number, number];
  /** x0 y0 x1 y1, paper mm. */
  box: [number, number, number, number];
}

/** The placed symbols of a sheet: the elements that name it and a symbol. */
export function symbolsOn(nodes: Map<string, NodeDto>, sheetId: string): NodeDto[] {
  const out: NodeDto[] = [];
  for (const n of nodes.values()) if (n.props.sheetId === sheetId && typeof n.props.symbolId === 'string' && Array.isArray(n.props.at)) out.push(n);
  return out;
}

/** What the symbol's preview is asked for: its values, flips and angle (the box is then scaled by its own scale). */
export function previewArgs(n: NodeDto): { values?: Record<string, string>; flipX?: boolean; flipY?: boolean; angle?: number } {
  const p = n.props;
  return {
    ...(p.values && typeof p.values === 'object' ? { values: p.values as Record<string, string> } : {}),
    ...(p.flipX ? { flipX: true } : {}), ...(p.flipY ? { flipY: true } : {}),
    ...(typeof p.angle === 'number' && p.angle ? { angle: p.angle } : {}),
  };
}

/** A key that says when a cached preview still holds. */
export const previewKey = (n: NodeDto): string => JSON.stringify([n.props.symbolId, previewArgs(n)]);

/** The symbol's box on the paper from its preview box (mm at scale 1, round the insertion point). */
export function boxOnPaper(n: NodeDto, preview: [number, number, number, number]): SheetSymbol {
  const at = n.props.at as number[];
  const s = typeof n.props.scale === 'number' && n.props.scale > 0 ? n.props.scale : 1;
  return { id: n.id, at: [at[0], at[1]], box: [at[0] + preview[0] * s, at[1] + preview[1] * s, at[0] + preview[2] * s, at[1] + preview[3] * s] };
}

/** The symbol under a paper point (the smallest box, `tol` mm round it; a tiny one stays easy to hit). */
export function pickSymbol(symbols: readonly SheetSymbol[], p: { x: number; y: number }, tol = 1.5): SheetSymbol | null {
  let best: SheetSymbol | null = null, least = Infinity;
  for (const s of symbols) {
    const [x0, y0, x1, y1] = s.box;
    if (p.x < x0 - tol || p.x > x1 + tol || p.y < y0 - tol || p.y > y1 + tol) continue;
    const a = (x1 - x0) * (y1 - y0);
    if (a < least) { least = a; best = s; }
  }
  return best;
}
