/**
 * "Edit layers" (forum topic 89, stage 2, docs/plan-layer-extents.md): an edit mode (the platform's edit scope) where a selected
 * wall's or floor's layers show their outlines and a grip on each of their edges, in plans and sections. A grip dragged (snapped
 * as every grip is, or a distance typed while dragging) moves its layer's edge along the way out of the element: one
 * architecture.setLayerExtent, one undo step. The engine says where the edges are (architecture.layerEdges, model coordinates);
 * here they are placed in the view: in a plan, the edges that move sideways; in a section, those that move in its plane, where
 * the cut crosses them (an element's end where the cut runs along its layer); in an elevation, the nearest of those at a point.
 */

import { Layers } from 'lucide-react';
import { create } from 'zustand';
import type { Grip, GripView, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { enterScope, exitScope, useScope } from '../../platform/scope';

export const LAYERS = 'Layers';
export const LAYER_HOSTS = new Set(['IfcWall', 'IfcSlab']);

export interface LayerEdgeGrip { layer: number; name: string; edge: string; index?: number; line: number[][]; dir: number[]; offset: number; own: boolean }
export interface LayerOutline { layer: number; name: string; footprint: number[][]; z0: number; z1: number }
export interface LayerEdges { layers: LayerOutline[]; grips: LayerEdgeGrip[] }
export interface ViewFrame3 { origin: number[]; right: number[]; up: number[]; dir: number[] }
/** How a view shows the model: a plan (no frame), or a section's or an elevation's frame. */
export interface ViewOf { frame?: ViewFrame3 | null; kind?: string | null; developed?: boolean }

/** The element being edited and its layers' edges as the engine last told them. */
export const useLayerEdit = create<{ id: string | null; data: LayerEdges | null }>(() => ({ id: null, data: null }));

export const layerEditOn = (): boolean => useScope.getState().scope?.kind === LAYERS;

/** The mode entered, the wall or floor selected kept selected. */
export function openLayerEdit(): void {
  const keep = useUi.getState().selected;
  if (!layerEditOn()) {
    enterScope({
      id: 'layer-edit', kind: LAYERS, label: 'Walls and floors',
      includes: (n: NodeDto) => LAYER_HOSTS.has(n.type),
      onDone: () => useLayerEdit.setState({ id: null, data: null }),
    });
    if (keep.length) useUi.setState({ selected: keep });
  }
  if (!keep.length) notify('Select a wall or a floor: its layers show a grip on each edge, in plans and sections.');
}

export const leaveLayerEdit = () => void exitScope();

export const editLayers: ToolDef = { id: 'architecture.editLayers', label: 'Edit layers', icon: Layers, shortcut: 'EL', run: () => openLayerEdit() };

// ─── Placing the edges in a view ──────────────────────────────────────

const dot3 = (a: number[], b: number[]) => a[0] * b[0] + a[1] * b[1] + (a[2] ?? 0) * (b[2] ?? 0);
const sub3 = (a: number[], b: number[]) => [a[0] - b[0], a[1] - b[1], (a[2] ?? 0) - (b[2] ?? 0)];
const mid3 = (pts: number[][]) => { const a = pts[0], b = pts[pts.length - 1]; return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, ((a[2] ?? 0) + (b[2] ?? 0)) / 2]; };
/** A model point as the view's drawing has it: (x, y) with the point origin + right·x + up·y. */
const inView = (f: ViewFrame3, p: number[]): Vec2 => { const d = sub3(p, f.origin); return { x: dot3(d, f.right), y: dot3(d, f.up) }; };

/** Where a polyline crosses the section's plane (through its origin, across its direction); null when it does not. */
export function crossing(f: ViewFrame3, line: number[][]): number[] | null {
  for (let i = 0; i + 1 < line.length; i++) {
    const [a, b] = [line[i], line[i + 1]];
    const [da, db] = [dot3(sub3(a, f.origin), f.dir), dot3(sub3(b, f.origin), f.dir)];
    if ((da > 1e-9 && db > 1e-9) || (da < -1e-9 && db < -1e-9) || Math.abs(da - db) < 1e-12) continue;
    const k = da / (da - db);
    return [a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k, (a[2] ?? 0) + ((b[2] ?? 0) - (a[2] ?? 0)) * k];
  }
  return null;
}

/** Whether a section's plane runs through a layer's footprint (its corners on both sides). */
export function straddles(f: ViewFrame3, footprint: number[][], z: number): boolean {
  const ds = footprint.map((p) => dot3(sub3([p[0], p[1], z], f.origin), f.dir));
  return Math.min(...ds) < -1e-9 && Math.max(...ds) > 1e-9;
}

/**
 * Where a grip of a layer's edge shows in a view, the way it moves there (a unit vector of the view) and how far the view
 * moves for a metre of the model along it; null when the edge does not show there or does not move in the view's plane.
 */
export function placeEdge(g: LayerEdgeGrip, layer: LayerOutline | undefined, v: ViewOf): { at: Vec2; along: Vec2; per: number; depth: number } | null {
  if (!g.line.length) return null;
  if (!v.frame) {
    // A plan: the edges moving sideways (a wall's ends, a floor's edges), at their middle.
    const h = Math.hypot(g.dir[0], g.dir[1]);
    if (h < 0.7) return null;
    const m = mid3(g.line);
    return { at: { x: m[0], y: m[1] }, along: { x: g.dir[0] / h, y: g.dir[1] / h }, per: h, depth: 0 };
  }
  if (v.developed) return null;
  const f = v.frame;
  const dv = { x: dot3(g.dir, f.right), y: dot3(g.dir, f.up) };
  const per = Math.hypot(dv.x, dv.y);
  if (per < 0.7) return null;
  const along = { x: dv.x / per, y: dv.y / per };
  let p: number[] | null = null;
  if (v.kind === 'section') {
    p = crossing(f, g.line);
    // An edge lying along the cut (a wall's end, the section running along the wall): where the cut runs through its layer.
    if (!p && layer && straddles(f, layer.footprint, (layer.z0 + layer.z1) / 2)) p = mid3(g.line);
  } else p = mid3(g.line);
  if (!p) return null;
  return { at: inView(f, p), along, per, depth: dot3(sub3(p, f.origin), f.dir) };
}

/** The new offset a grip dropped at `to` gives its edge: its offset now and the drag along its way, in mm, within ±1 m. */
export function droppedOffset(offset: number, at: Vec2, along: Vec2, per: number, to: Vec2): number {
  const moved = ((to.x - at.x) * along.x + (to.y - at.y) * along.y) / per;
  return Math.max(-1, Math.min(1, Math.round((offset + moved) * 10000) / 10000));
}

const EDGE_NAMES: Record<string, string> = { start: 'start', end: 'end', bottom: 'bottom', top: 'top' };

/** The grips of the element's layers' edges in a view; in an elevation, of the edges at one point only the nearest's. */
export function layerGrips(node: NodeDto, data: LayerEdges, view: GripView | undefined): Grip[] {
  const v: ViewOf = { frame: view?.frame ?? null, kind: view?.kind ?? null, developed: view?.developed };
  const placed = data.grips.flatMap((g) => {
    const p = placeEdge(g, data.layers.find((l) => l.layer === g.layer), v);
    return p ? [{ g, ...p }] : [];
  });
  const kept = v.frame && v.kind !== 'section'
    ? placed.filter((a) => !placed.some((b) => b !== a && Math.hypot(b.at.x - a.at.x, b.at.y - a.at.y) < 1e-3 && (b.depth < a.depth - 1e-9 || (Math.abs(b.depth - a.depth) <= 1e-9 && placed.indexOf(b) < placed.indexOf(a)))))
    : placed;
  return kept.map(({ g, at, along, per }) => {
    const edge = g.edge === 'edge' ? `edge ${(g.index ?? 0) + 1}` : EDGE_NAMES[g.edge] ?? g.edge;
    const mm = Math.round(g.offset * 1000 * 10) / 10;
    return {
      key: `layer:${g.layer}:${g.edge}:${g.index ?? ''}`,
      at, kind: 'edgeMid' as const,
      title: `${g.layer + 1} · ${g.name} · ${edge} · ${mm > 0 ? '+' : ''}${mm} mm — drag its edge`,
      ...(v.frame ? { view: view?.view } : {}),
      // Kept on its way: the edge moves only out of the element or into it.
      constrain: (to: Vec2) => { const k = (to.x - at.x) * along.x + (to.y - at.y) * along.y; return { x: at.x + along.x * k, y: at.y + along.y * k }; },
      step: (to: Vec2) => {
        const offset = droppedOffset(g.offset, at, along, per, to);
        if (Math.abs(offset - g.offset) < 1e-5) return null;
        // Its cut left as it is (not given, the engine keeps it).
        return { command: 'architecture.setLayerExtent', args: { id: node.id, layer: g.layer, edge: g.edge, ...(g.edge === 'edge' ? { index: g.index ?? 0 } : {}), offset } };
      },
    };
  });
}

/** Whether the element's grips are its layers' now: the mode on, its edges told. */
export function editingLayersOf(node: NodeDto): LayerEdges | null {
  if (!layerEditOn() || !LAYER_HOSTS.has(node.type)) return null;
  const s = useLayerEdit.getState();
  return s.id === node.id ? s.data : null;
}

/** A layer's outline in a view: its footprint in a plan; in a section, where the cut runs through it; in an elevation, its extent seen. */
export function outlineInView(l: LayerOutline, v: ViewOf): Vec2[][] {
  if (!v.frame) return [l.footprint.map((p) => ({ x: p[0], y: p[1] }))];
  if (v.developed) return [];
  const f = v.frame;
  const rect = (u0: number, u1: number): Vec2[] => {
    const a = inView(f, [0, 0, l.z0]).y, b = inView(f, [0, 0, l.z1]).y;
    const [y0, y1] = [Math.min(a, b), Math.max(a, b)];
    return [{ x: u0, y: y0 }, { x: u1, y: y0 }, { x: u1, y: y1 }, { x: u0, y: y1 }];
  };
  // Along the view, at the footprint's height (the frame's x does not change with z in an upright view).
  const u = (p: number[]) => inView(f, [p[0], p[1], f.origin[2] ?? 0]).x;
  if (v.kind !== 'section') {
    const us = l.footprint.map(u);
    return [rect(Math.min(...us), Math.max(...us))];
  }
  const cuts: number[] = [];
  const n = l.footprint.length;
  for (let i = 0; i < n; i++) {
    const p = crossing(f, [[...l.footprint[i], f.origin[2] ?? 0], [...l.footprint[(i + 1) % n], f.origin[2] ?? 0]]);
    if (p) cuts.push(u(p));
  }
  cuts.sort((a, b) => a - b);
  const o: Vec2[][] = [];
  for (let i = 0; i + 1 < cuts.length; i += 2) if (cuts[i + 1] - cuts[i] > 1e-6) o.push(rect(cuts[i], cuts[i + 1]));
  return o;
}
