/**
 * What a strip footing is drawn with (decision 427, a tester's report #116): the concrete footing alone, the default, or
 * with the wall (the stem) of a strip type on it. The tool's option in the command line's bar; the choice is kept on this
 * computer, as the layer or the reference line of other tools are for the session.
 */

import { create } from 'zustand';
import { useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';

const KEY = 'lapin.strip.wall';

interface Prefs { wall: boolean; typeId: string | null }

function read(): Prefs {
  try {
    const v = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Partial<Prefs> | null;
    if (v && typeof v === 'object') return { wall: v.wall === true, typeId: typeof v.typeId === 'string' ? v.typeId : null };
  } catch { /* the default */ }
  return { wall: false, typeId: null };
}

/** The choice: no wall (false) unless one is asked for. */
export const useStripPrefs = create<Prefs>(() => read());

const remember = (p: Prefs) => { try { localStorage.setItem(KEY, JSON.stringify(p)); } catch { /* kept for this session only */ } };

/** The strip types with a wall on them (a stem with its layers, not a swept profile). */
export const wallTypes = (nodes: ReadonlyMap<string, NodeDto>): NodeDto[] =>
  [...nodes.values()].filter((t) => t.type === 'IfcFootingType' && t.props.kind === 'strip' && !t.props.profile);

/** What the engine's addStripFooting is asked: the footing alone, or with the wall of the type chosen (the first strip type with a wall when none is). */
export function stripArgs(nodes: ReadonlyMap<string, NodeDto>): Record<string, unknown> {
  const { wall, typeId } = useStripPrefs.getState();
  if (!wall) return { stem: false };
  return typeId && nodes.get(typeId) ? { typeId } : { stem: true };
}

/** Footing alone / with wall, and the wall's type, in the command line's bar while a strip footing tool is on. */
export function StripOption() {
  const { wall, typeId } = useStripPrefs();
  const nodes = useSession((s) => s.nodes);
  const types = wallTypes(nodes);
  const set = (p: Prefs) => { remember(p); useStripPrefs.setState(p); };
  return (
    <span className="cad-layer on">
      <select aria-label="Wall on the footing" data-testid="strip-wall" title="Draw the concrete footing alone, or with the wall (the stem) of a strip type on it"
        value={wall ? 'wall' : 'alone'} onChange={(e) => set({ wall: e.target.value === 'wall', typeId })}>
        <option value="alone">Footing alone</option>
        <option value="wall">With wall</option>
      </select>
      {wall && types.length > 0 && (
        <select aria-label="Strip type with a wall" data-testid="strip-wall-type" value={typeId && types.some((t) => t.id === typeId) ? typeId : ''} onChange={(e) => set({ wall: true, typeId: e.target.value || null })}>
          <option value="">First type with a wall</option>
          {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
      )}
    </span>
  );
}
