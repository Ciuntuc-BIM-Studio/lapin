/**
 * Point clouds in the interface (docs/point-clouds.md): which links are clouds, how each is drawn in a view (its link's
 * settings, then the view's Point clouds tab: shown, halftone, colours, classes), and the state of their indexes as the
 * engine reports it, for Manage → Links and the 3D view's chip.
 */

import { create } from 'zustand';
import type { NodeDto } from '../engine/protocol';
import type { CloudSpec, ColorMode } from './pointClouds3d';

export const isCloudLink = (n: NodeDto | undefined): boolean => !!n && n.type === 'LapinLink' && n.props.kind === 'pointcloud';

export interface CloudStatus { state: string; stage?: string; done?: number; error?: string; drawn?: number }

export const usePointClouds = create<{ status: Record<string, CloudStatus> }>(() => ({ status: {} }));

export function setCloudStatus(id: string, s: CloudStatus): void {
  const cur = usePointClouds.getState().status[id];
  const next = s.state === 'ready' && s.drawn == null && cur?.drawn != null ? { ...s, drawn: cur.drawn } : s;
  if (cur && cur.state === next.state && cur.stage === next.stage && cur.done === next.done && cur.drawn === next.drawn && cur.error === next.error) return;
  usePointClouds.setState((st) => ({ status: { ...st.status, [id]: next } }));
}

/** A view's own settings for one cloud (its graphics' `pointClouds`): what it overrides of the link's. */
export interface ViewCloud { hidden?: boolean; halftone?: boolean; colorMode?: ColorMode; hiddenClasses?: number[] }

/** The graphics' point cloud rows of a view node (its own graphics; a set's are resolved by the engine for 2D). */
export function viewClouds(view: NodeDto | undefined): Record<string, ViewCloud> {
  const g = view?.props.graphics as { pointClouds?: Record<string, ViewCloud> } | undefined;
  return g?.pointClouds && typeof g.pointClouds === 'object' ? g.pointClouds : {};
}

const MODES: ColorMode[] = ['rgb', 'intensity', 'elevation', 'classification'];

/** How every cloud link is drawn: the link's own (shown, halftone, colours, size), the view's rows over them. */
export function cloudSpecs(nodes: ReadonlyMap<string, NodeDto>, view?: Record<string, ViewCloud>): CloudSpec[] {
  const out: CloudSpec[] = [];
  for (const n of nodes.values()) {
    if (!isCloudLink(n)) continue;
    const v = view?.[n.id] ?? {};
    const mode = (v.colorMode ?? n.props.colorMode) as ColorMode;
    out.push({
      id: n.id,
      rev: JSON.stringify([n.props.key ?? null, n.props.placement ?? null]),
      shown: n.props.visible !== false && n.props.in3d !== false && v.hidden !== true,
      halftone: n.props.halftone === true || v.halftone === true,
      colorMode: MODES.includes(mode) ? mode : 'rgb',
      pointSize: typeof n.props.pointSize === 'number' ? n.props.pointSize : 2,
      hiddenClasses: Array.isArray(v.hiddenClasses) ? v.hiddenClasses.filter((k) => Number.isInteger(k)) : [],
    });
  }
  return out.sort((a, b) => a.id.localeCompare(b.id));
}

/** A cloud's state in a few words, for Manage → Links. */
export function statusText(s: CloudStatus | undefined): string {
  if (!s) return 'Waiting…';
  if (s.state === 'indexing') return `Indexing: ${s.stage ?? ''} ${Math.round((s.done ?? 0) * 100)}%`;
  if (s.state === 'failed' || s.state === 'missing') return s.error ?? 'Not available';
  if (s.state === 'ready') return s.drawn != null ? `${s.drawn.toLocaleString()} points drawn` : 'Ready';
  return s.state;
}
