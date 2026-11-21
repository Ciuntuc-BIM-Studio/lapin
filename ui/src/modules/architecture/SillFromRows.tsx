/**
 * The "Sill measured from" choice of a door, window or opening (forum topic 192, decision 749): the structural level
 * of its wall's base, or the finished floor over it. Choosing it alone keeps the element where it is; the sill number
 * is read against the new reference. A hint says when the wall's finished floor is still 0.
 */

import { typeOf } from '../../engine/model';
import { useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { finishedFloorOf, floorHintShown, type SillFrom } from './sillFrom';

export const SILL_FROM_TIP = 'What the sill height is measured from: the wall\'s structural base, or the finished floor above it. Changing it keeps the opening where it is and reads the number from the new reference.';
export const FLOOR_HINT = 'The finished floor is 0 until "Finished floor above base" is set on the wall or its wall type.';

export function SillFromRows({ value, wall, onChange, disabled }: { value: SillFrom; wall: NodeDto | null; onChange: (v: SillFrom) => void; disabled?: boolean }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const type = wall ? typeOf({ nodes, relations }, wall.id) : null;
  const floor = finishedFloorOf(wall, type);
  return (
    <>
      <dt title={SILL_FROM_TIP}>Sill measured from</dt>
      <dd>
        <select data-field="sill-from" value={value} disabled={disabled} title={SILL_FROM_TIP} onChange={(e) => onChange(e.target.value === 'finished' ? 'finished' : 'structural')}>
          <option value="structural">Structural level</option>
          <option value="finished">Finished floor</option>
        </select>
        {wall && floorHintShown(value, floor) && <p className="muted small" data-field="sill-floor-hint">{FLOOR_HINT}</p>}
      </dd>
    </>
  );
}
