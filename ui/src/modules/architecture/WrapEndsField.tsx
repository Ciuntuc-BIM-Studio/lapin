/**
 * Which face's finish wraps a wall's free ends (in-app report #192, decision 859): on a wall type (its walls' default) and on a
 * wall (its own, or as its type). The engine builds it (`WallEndWraps`): the face's outer layer caps each free end, the other
 * layers stop its thickness short; joined ends are the joins'.
 */

export type WrapEnds = 'none' | 'first' | 'last';

export const WRAP_TIP = 'At a free end (no wall joined there) the outer layer of the chosen face turns the end, from its face to the other one, and the other layers stop its thickness short: the end shows the finish, not the core. Joined ends are the join\'s. None: each layer shows its own end, as IFC lays it.';

/** A stored value, read back: one of the three, else null (none set). */
export const wrapOf = (v: unknown): WrapEnds | null => (v === 'none' || v === 'first' || v === 'last' ? v : null);

/** The select: `inherit` adds "As its type" (the value null), for a wall. */
export function WrapEndsField({ value, onChange, inherit, disabled }: { value: WrapEnds | null; onChange: (v: WrapEnds | null) => void; inherit?: boolean; disabled?: boolean }) {
  return (
    <select data-testid="wrap-ends" title={WRAP_TIP} value={value ?? (inherit ? '' : 'none')} disabled={disabled}
      onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as WrapEnds))}>
      {inherit && <option value="">As its type</option>}
      <option value="none">None</option>
      <option value="first">Layer 1's face (R)</option>
      <option value="last">Last layer's face (L)</option>
    </select>
  );
}
