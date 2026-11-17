/**
 * Scale to a known distance (decision 371; forum 143, as simply as in Edificius): an image to trace, or an imported PDF's
 * group (its lines and pictures), grown or shrunk about the first of two points so that they lie their true distance apart.
 * The distance is shown while the second point is picked and its true value typed there (2400, 50m, 3000+250, in the
 * project's unit unless it says its own); clicked without one, it is asked for after. Offered right after an import, on the
 * right-click menu of a selected image or PDF and in its properties.
 */

import type { NodeDto, RelationDto } from '../../engine/protocol';
import { GROUP, membersOf } from '../../platform/groups';
import { notify, useUi } from '../../app/store';

type Relations = ReadonlyMap<string, RelationDto>;

export const SCALE_IMAGE_TOOL = 'drafting.scaleImage';
export const IMAGE = 'LapinViewImage';

/**
 * Whether the tool scales this: an image, or a group of images and drawing on a view only (an imported PDF's). The engine
 * checks the same (drafting.calibrateImage).
 */
export function scalableTrace(id: string, nodes: ReadonlyMap<string, NodeDto>, relations?: Relations): boolean {
  const n = nodes.get(id);
  if (!n) return false;
  if (n.type === IMAGE) return true;
  if (n.type !== GROUP) return false;
  const members = membersOf(id, relations);
  const traced = (m: string) => { const t = nodes.get(m)?.type; return t === IMAGE || t === 'IfcAnnotation'; };
  return members.length > 0 && members.every(traced);
}

/** What the tool scales among the selection: the image or the PDF's group selected (an image first), else null. */
export function scaleTarget(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto>, relations?: Relations): string | null {
  return selected.find((id) => nodes.get(id)?.type === IMAGE) ?? selected.find((id) => scalableTrace(id, nodes, relations)) ?? null;
}

/** The tool started on `id` (selected): two points on it, then the true distance; Esc leaves it as it is. */
export function startScaleImage(id: string): void {
  useUi.setState({ selected: [id], selection: id, tool: SCALE_IMAGE_TOOL });
  notify('Scale to a known distance: click two points a known distance apart and type their true distance (Esc skips).');
}

/**
 * The true distance: the one typed at the cursor (m), else the answer asked for, read by `parse` (metres when it says no
 * unit); null when there is none, or it is not above 0.
 */
export function trueDistance(typed: number | undefined, ask: () => string | null, parse: (s: string) => number | null): number | null {
  const v = typed ?? (() => { const a = ask(); return a == null ? null : parse(a); })();
  return v != null && v > 0 ? v : null;
}
