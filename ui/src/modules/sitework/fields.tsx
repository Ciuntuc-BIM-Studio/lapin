/** The small fields the site works' dialogs and sections share, over the interface's own: a length, a list of lengths, a number. */

import { useStandards } from '../../engine/unitsStore';
import { Cell, LengthField, NumberField } from '../../shell/cells';
import { formatLengths, parseLengths } from '../core/format';

/**
 * A length in the project's unit, committed on Enter or blur; `blank` (what
 * empty means, shown as the placeholder) lets empty commit null. `unit`
 * false leaves the unit's label off. `min` in mm (as the dialogs give it).
 */
export function Mm({ value, onCommit, min, blank, unit = true }: { value: number | null | undefined; onCommit: (m: number | null) => void; min?: number; blank?: string; unit?: boolean }) {
  return <LengthField metres={value} min={min != null ? min / 1000 : undefined} blank={blank != null} placeholder={blank} noUnit={!unit} onCommit={(m) => { if (m != null || blank != null) onCommit(m); }} />;
}

/** Lengths separated by spaces or semicolons (a catalogue: 3070 2570 2070), in the project's unit. */
export function MmList({ value, onCommit }: { value: number[]; onCommit: (v: number[]) => void }) {
  const units = useStandards((s) => s.units);
  return <Cell value={formatLengths(value, units)} title="Lengths separated by spaces or semicolons"
    onCommit={(t) => { const l = parseLengths(t, units)?.filter((x) => x > 0); if (!l?.length) return false; onCommit(l); }} />;
}

/** A plain number, committed on Enter or blur. */
export function Num({ value, onCommit, min }: { value: number; onCommit: (v: number) => void; step?: number; min?: number }) {
  return <NumberField value={value} min={min} onCommit={(n) => { if (n != null) onCommit(n); }} />;
}
