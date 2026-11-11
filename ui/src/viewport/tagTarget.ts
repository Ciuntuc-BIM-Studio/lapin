/**
 * What a tag tags, shown (report #130): while the TAG command hovers an element, and while a placed tag is hovered or
 * selected, the element it tags is highlighted in the 2D views. The views draw the set {@link tagTargets} gives, in the
 * accent colour (`tag-target`), under the selection's own highlight.
 */

import { create } from 'zustand';
import type { NodeDto } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { useCad } from './cad/session';

interface Hover { element: string | null; tag: string | null }
export const useTagHover = create<Hover>(() => ({ element: null, tag: null }));

const isTag = (n: NodeDto | undefined): n is NodeDto => n?.props.predefinedType === 'TAG';

/** The element a tag node tags. */
export const taggedBy = (n: NodeDto | undefined): string | null => (isTag(n) && typeof n.props.element === 'string' ? n.props.element : null);

/** The elements to highlight: those the selected tags tag, the hovered tag's, and the one the TAG command hovers. */
export function tagTargets(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto>, hover: Hover): Set<string> {
  const out = new Set<string>();
  for (const id of selected) { const e = taggedBy(nodes.get(id)); if (e) out.add(e); }
  if (hover.tag) { const e = taggedBy(nodes.get(hover.tag)); if (e) out.add(e); }
  if (hover.element) out.add(hover.element);
  return out;
}

/** Whether the command line waits for an element to tag. */
export function tagToolPicking(): boolean {
  const s = useCad.getState();
  return s.command === 'TAG' && s.step?.want === 'select';
}

/**
 * The cursor moved to `p`: the element the TAG command would tag, else the tag under it. `pickElement` and `pickTag` are the
 * view's own picks; `anyTags` says whether the view has tags at all, so a plan with none is never searched.
 */
export function trackTagHover(p: Vec2 | null, o: { pickElement: (p: Vec2) => string | null; pickTag: (p: Vec2) => string | null; anyTags: boolean; busy: boolean }) {
  let element: string | null = null, tag: string | null = null;
  if (p && tagToolPicking()) {
    const id = o.pickElement(p);
    element = id && !id.includes('#') ? id : null;
  } else if (p && o.anyTags && !o.busy) tag = o.pickTag(p);
  const cur = useTagHover.getState();
  if (cur.element !== element || cur.tag !== tag) useTagHover.setState({ element, tag });
}

/** Whether any of `nodes` is a tag on the view `viewId`. */
export function viewHasTags(nodes: ReadonlyMap<string, NodeDto>, viewId: string | null | undefined): boolean {
  if (!viewId) return false;
  for (const n of nodes.values()) if (isTag(n) && n.props.viewId === viewId) return true;
  return false;
}
