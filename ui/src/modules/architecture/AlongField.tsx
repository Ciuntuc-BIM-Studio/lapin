/**
 * An opening's "Along the wall" in Properties: its distance along the wall from a chosen reference (the wall's start, the
 * nearest face of the wall meeting it, that wall's core face) to a chosen point of the opening (its start, centre or end).
 * Both choices are views of the same number: read through architecture.openingReferences, written through
 * architecture.moveOpening (the opening's centre), and remembered per user (prefs), never stored in the model.
 */

import { useEffect, useState } from 'react';
import { usePrefs, setPrefs } from '../../app/prefs';
import { execute, query, useSession } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import type { OpeningRefs } from './openingDims';
import { alongScale, alongShown, alongWritten, referenceFor, type AlongFrom, type AlongPoint } from './openingAlong';

/** The references of the opening `id`, asked again when the model changes (null until answered). */
function useReferences(id: string): OpeningRefs | null {
  const nodes = useSession((s) => s.nodes);
  const [refs, setRefs] = useState<OpeningRefs | null>(null);
  useEffect(() => {
    let current = true;
    void query<OpeningRefs>('architecture.openingReferences', { id }).then((r) => { if (current) setRefs(r); }, () => { if (current) setRefs(null); });
    return () => { current = false; };
  }, [id, nodes]);
  return refs;
}

const FROM_LABELS: Record<AlongFrom, string> = { start: 'Wall start', face: 'Meeting wall’s face', core: 'Meeting wall’s core face' };
const POINT_LABELS: Record<AlongPoint, string> = { start: 'Opening start', centre: 'Opening centre', end: 'Opening end' };

/** `id` the opening to move (the opening, or the door or window filling it); `along` and `width` its present place and size (m). */
export function AlongField({ id, along, width, editable = true }: { id: string; along: number; width: number; editable?: boolean }) {
  const refs = useReferences(id);
  const { from, point } = usePrefs((s) => s.openingAlong);
  const ref = referenceFor(refs, from, along, width, point);
  const scale = alongScale(refs);
  return (
    <span className="along-field" data-field="along">
      <LengthField metres={alongShown(ref, along, width, point, scale)} readOnly={!editable} testId="along-value"
        title={ref.name ? ref.name : undefined}
        onCommit={(v) => { if (v != null) void execute('architecture.moveOpening', { id, along: alongWritten(ref, v, width, point, scale) }); }} />
      <select data-field="along-from" title="Measured from" value={from} onChange={(e) => setPrefs({ openingAlong: { from: e.target.value as AlongFrom, point } })}>
        {(Object.keys(FROM_LABELS) as AlongFrom[]).map((k) => <option key={k} value={k}>{FROM_LABELS[k]}</option>)}
      </select>
      <select data-field="along-point" title="Measured to" value={point} onChange={(e) => setPrefs({ openingAlong: { from, point: e.target.value as AlongPoint } })}>
        {(Object.keys(POINT_LABELS) as AlongPoint[]).map((k) => <option key={k} value={k}>{POINT_LABELS[k]}</option>)}
      </select>
    </span>
  );
}
