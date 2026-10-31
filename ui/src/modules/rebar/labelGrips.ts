/**
 * The grips of one rebar label as the engine drew it (decisions 221, 222): its point (the balloon's), its leader's elbow
 * when it has one or, without one, the leader's middle, which dragged makes the elbow, and a distribution's string middle,
 * which dragged across the bars changes the string's offset. Each drag is one patch of the label's place; the caller says
 * which command carries it (update, updateRun or placeLabel) and how the points map through an inset.
 */

import type { Grip, GripStep, Vec2 } from '../../platform/module';
import { pt } from '../../viewport/gripKit';

/** What the engine tells of a label in a mark's data (`label`, or an entry of `labels`). */
export interface LabelData {
  at?: number[];
  elbow?: number[];
  /** The leader's middle, when there is no elbow yet. */
  leader?: number[];
  /** A distribution's string: its middle, the outward direction across the bars, its offset (paper mm) and a paper mm in the view's units. */
  string?: { mid: number[]; dir: number[]; offset: number; mm: number };
}

/** A label's place patched: each field set, or null to take it away. */
export interface LabelPatch { at?: number[] | null; elbow?: number[] | null; offset?: number | null }

export interface LabelMapping { show: (p: Vec2) => Vec2; back: (p: Vec2) => Vec2 }
const same: LabelMapping = { show: (p) => p, back: (p) => p };

/** The grips of a label; `key` prefixes theirs (`label`, `label:stirrups`), `step` makes the command of a patch. */
export function labelPlaceGrips(label: LabelData | undefined, key: string, step: (patch: LabelPatch) => GripStep | null, map: LabelMapping = same): Grip[] {
  if (!label) return [];
  const grips: Grip[] = [];
  const { show, back } = map;
  const xy = (p: Vec2) => [p.x, p.y];
  const at = pt(label.at);
  if (at) grips.push({ key, kind: 'move', at: show(at), title: 'Drag: move the label', step: (to) => step({ at: xy(back(to)) }) });
  const elbow = pt(label.elbow);
  if (elbow) grips.push({ key: `${key}:elbow`, kind: 'vertex', at: show(elbow), title: 'Drag: move the elbow · Ctrl/⌘-click takes it away', step: (to) => step({ elbow: xy(back(to)) }), remove: () => step({ elbow: null }) });
  else {
    const mid = pt(label.leader);
    if (mid) grips.push({ key: `${key}:elbow`, kind: 'edgeMid', at: show(mid), title: 'Drag: bend the leader here', step: (to) => step({ elbow: xy(back(to)) }) });
  }
  const s = label.string;
  const mid = pt(s?.mid), dir = pt(s?.dir);
  if (s && mid && dir && s.mm > 0) {
    grips.push({
      key: `${key}:string`, kind: 'edgeMid', at: show(mid), title: 'Drag: move the string off the bars',
      step: (to) => { const p = back(to); return step({ offset: Math.round((s.offset + ((p.x - mid.x) * dir.x + (p.y - mid.y) * dir.y) / s.mm) * 2) / 2 }); },
    });
  }
  return grips;
}
