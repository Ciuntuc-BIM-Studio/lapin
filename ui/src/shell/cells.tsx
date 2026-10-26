/**
 * The properties panel's cells, with a spreadsheet's feel: a cell commits on
 * Enter (and moves down) or when it loses focus, Escape puts its value back,
 * the arrows move between cells, focusing one selects its text. Lengths are
 * typed in mm (or with a unit: 240cm, 2.4m) and numbers may be sums.
 */

import { useEffect, useState } from 'react';
import { parseLength } from '../viewport/liveInput';
import { formatLength, unitLabel, useStandards } from '../engine/unitsStore';

/** A cell: commits on blur (Enter blurs through the panel's keys), Escape restores. */
export function Cell({ value, onCommit, suffix, placeholder, className, readOnly }: {
  value: string; onCommit: (text: string) => void; suffix?: string; placeholder?: string; className?: string; readOnly?: boolean;
}) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <span className="cell-wrap">
      <input className={`cell${className ? ` ${className}` : ''}`} value={text} placeholder={placeholder} readOnly={readOnly}
        onChange={(e) => setText(e.target.value)}
        onBlur={() => { if (text !== value) onCommit(text); }}
        onKeyDown={(e) => { if (e.key === 'Escape') { setText(value); e.preventDefault(); } }} />
      {suffix && <span className="cell-unit">{suffix}</span>}
    </span>
  );
}

/** A length cell: shown in the project's unit (mm, cm, m, feet and inches, inches), typed in it or with a unit of its own; empty clears it. */
export function LengthCell({ metres, onCommit, placeholder }: { metres: number | null | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  const u = useStandards((s) => s.units);
  return (
    <Cell className="num" value={typeof metres === 'number' ? formatLength(metres, u) : ''} suffix={unitLabel(u) || undefined} placeholder={placeholder}
      onCommit={(t) => { if (!t.trim()) { onCommit(null); return; } const m = parseLength(t); if (m != null) onCommit(m); }} />
  );
}
