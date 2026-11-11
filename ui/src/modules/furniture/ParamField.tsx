/** One of a furniture kind's parameters, as its kind says it is edited (the types dialog and the casework configurator). */

import { LengthField, NumberField } from '../../shell/cells';
import { CHOICE_LABELS, type KindParam } from './furniture';

export function ParamField({ p, value, onCommit }: { p: KindParam; value: unknown; onCommit: (v: unknown) => void }) {
  switch (p.kind) {
    case 'length': return <LengthField metres={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'number': return <NumberField value={Number(value ?? 0)} min={p.min ?? undefined} max={p.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'choice': return <select value={String(value ?? '')} onChange={(e) => onCommit(e.target.value)}>{(p.choices ?? []).map((c) => <option key={c} value={c}>{CHOICE_LABELS[c] ?? c}</option>)}</select>;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    default: return null;
  }
}
