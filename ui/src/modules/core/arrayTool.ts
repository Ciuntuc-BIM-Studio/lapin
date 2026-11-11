/**
 * Modify → Array (decision 448): copies of the selection in a row (a step along x, y and z) or round a centre (an angle
 * filled), any element alike: a wall, a column, a group, an imported mesh's chair. One undo step (element.array).
 */
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ask, choiceField, lengthField, numberField } from '../../shell/AskBox';

/** What an array copies: the selected elements and groups, not a drafted shape (its view's tools copy those). */
export function arrayable(): string[] {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => {
    const n = nodes.get(id);
    return !!n && (!!n.storeyId || n.type === 'IfcGroup') && n.props.predefinedType !== 'DRAFT';
  });
}

export async function arraySelection(): Promise<void> {
  const ids = arrayable();
  if (!ids.length) { notify('Select what to array first, then Array.'); return; }
  const r = await ask('Array', [
    choiceField('kind', 'Kind', 'row', [{ value: 'row', label: 'In a row' }, { value: 'polar', label: 'Round a centre' }]),
    numberField('count', 'Copies (with the first)', 3),
    lengthField('dx', 'Step x', 1), lengthField('dy', 'Step y', 0), lengthField('dz', 'Step z', 0),
    numberField('angle', 'Angle filled (°)', 360), lengthField('cx', 'Centre x', 0), lengthField('cy', 'Centre y', 0),
  ]);
  if (!r) return;
  if (r.kind === 'polar') await execute('element.array', { ids, kind: 'polar', count: Number(r.count), angle: Number(r.angle), center: [Number(r.cx), Number(r.cy)] });
  else await execute('element.array', { ids, count: Number(r.count), step: [Number(r.dx), Number(r.dy), Number(r.dz)] });
}
