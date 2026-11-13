/**
 * Each end of a wall, a beam or a strip footing joining automatically or not (the forum's topic #50, decision 546). Not
 * stored, an end of an element drawn in Lapin joins and one recognised from an IFC file does not (the import reproduces the
 * file); `autoJoin: {start, end}` says otherwise. The engine's `element.setAutoJoin` sets it.
 */

import type { Grip } from '../../platform/module';

export type PathEnd = 'start' | 'end';

type Props = Record<string, unknown>;

/** Whether the element's end joins automatically. */
export function autoJoinAt(props: Props, end: PathEnd): boolean {
  const o = props.autoJoin;
  if (o && typeof o === 'object' && typeof (o as Record<string, unknown>)[end] === 'boolean') return (o as Record<string, boolean>)[end];
  return props.imported == null;
}

/** Whether the element is a path element with both ends (what the toggles apply to). */
export const isPathElement = (props: Props): boolean => Array.isArray(props.start) && Array.isArray(props.end);

/** The grips with a right-click item on each end: not joining there, or joining again. */
export function withAutoJoinMenus(id: string, props: Props, grips: Grip[]): Grip[] {
  return grips.map((g) => {
    if (g.key !== 'start' && g.key !== 'end') return g;
    const end = g.key;
    const on = autoJoinAt(props, end);
    return {
      ...g,
      menu: [...(g.menu ?? []), {
        label: on ? "Don't join at this end" : 'Join automatically at this end',
        run: () => ({ command: 'element.setAutoJoin', args: { ids: [id], [end]: !on } }),
      }],
    };
  });
}
