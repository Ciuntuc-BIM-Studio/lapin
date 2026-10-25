/**
 * The properties panel's cells, with a spreadsheet's feel: a cell commits on
 * Enter (and moves down) or when it loses focus, Escape puts its value back,
 * the arrows move between cells, focusing one selects its text. Lengths are
 * typed in mm (or with a unit: 240cm, 2.4m) and numbers may be sums.
 */

import { useEffect, useState } from 'react';
import { parseLength } from '../viewport/liveInput';

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

/** A length cell: shown in mm, typed in mm or with a unit; empty clears it. */
export function LengthCell({ metres, onCommit, placeholder }: { metres: number | null | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  return (
    <Cell className="num" value={typeof metres === 'number' ? String(Math.round(metres * 1000)) : ''} suffix="mm" placeholder={placeholder}
      onCommit={(t) => { if (!t.trim()) { onCommit(null); return; } const m = parseLength(t); if (m != null) onCommit(m); }} />
  );
}
