/**
 * The properties panel's cells, with a spreadsheet's feel: a cell commits on
 * Enter (and moves down) or when it loses focus, Escape puts its value back,
 * the arrows move between cells, focusing one selects its text. Lengths are
 * typed in the project's unit (or with a unit: 240cm, 2.4m) and numbers may
 * be sums. LengthField and NumberField are the interface's one length and
 * number field, in the panel and outside it.
 */

import { useEffect, useState, type CSSProperties } from 'react';
import { formatLength, unitLabel, useStandards } from '../engine/unitsStore';
import { commitLength, commitNumber, NO_COMMIT, type CommitRules } from './fieldCommit';

/** A cell: commits on blur or Enter, Escape restores; an onCommit that returns false (refused) puts the value back. */
export function Cell({ value, onCommit, suffix, placeholder, className, readOnly, title, style, testId }: {
  value: string; onCommit: (text: string) => boolean | void; suffix?: string; placeholder?: string; className?: string; readOnly?: boolean; title?: string;
  /** The input's own style (a width in a row of fields). */
  style?: CSSProperties;
  /** The input's data-field, for the guide's scenes and tests. */
  testId?: string;
}) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <span className="cell-wrap" style={style ? { width: 'auto' } : undefined}>
      <input className={`cell${className ? ` ${className}` : ''}`} value={text} placeholder={placeholder} readOnly={readOnly} title={title} style={style} data-field={testId}
        onChange={(e) => setText(e.target.value)}
        onBlur={() => { if (text !== value && onCommit(text) === false) setText(value); }}
        // Enter blurs (which commits); not prevented, so the properties panel's keys still move down.
        onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') { setText(value); e.preventDefault(); } }} />
      {suffix && <span className="cell-unit">{suffix}</span>}
    </span>
  );
}

/** What the shared length and number fields take besides their value. */
export interface FieldProps extends CommitRules {
  placeholder?: string; className?: string; readOnly?: boolean; title?: string; style?: CSSProperties; testId?: string;
}

/**
 * The one length field: shown and typed in the project's unit (mm, cm, m,
 * feet and inches, inches) or with a unit of its own (240cm, 2.4m, 3'6"),
 * sums allowed; one commit on blur or Enter (one undo step), only when the
 * value changes; empty or unreadable text is put back unless `blank` (then
 * empty commits null: the default). `min` and `max` are in metres.
 */
export function LengthField({ metres, onCommit, placeholder, className, readOnly, title, style, testId, ...rules }: FieldProps & { metres: number | null | undefined; onCommit: (m: number | null) => void }) {
  const u = useStandards((s) => s.units);
  const shown = typeof metres === 'number' ? formatLength(metres, u) : '';
  return (
    <Cell value={shown} className={`num${className ? ` ${className}` : ''}`} suffix={unitLabel(u) || undefined} placeholder={placeholder} readOnly={readOnly} title={title} style={style} testId={testId}
      onCommit={(t) => { const v = commitLength(t, metres, rules); if (v !== NO_COMMIT) onCommit(v); return v !== NO_COMMIT; }} />
  );
}

/** The one number field: sums allowed, a decimal comma too; the same commit rules as LengthField. */
export function NumberField({ value, onCommit, suffix, placeholder, className, readOnly, title, style, testId, ...rules }: FieldProps & { value: number | null | undefined; onCommit: (n: number | null) => void; suffix?: string }) {
  return (
    <Cell value={typeof value === 'number' ? String(value) : ''} className={`num${className ? ` ${className}` : ''}`} suffix={suffix} placeholder={placeholder} readOnly={readOnly} title={title} style={style} testId={testId}
      onCommit={(t) => { const v = commitNumber(t, value, rules); if (v !== NO_COMMIT) onCommit(v); return v !== NO_COMMIT; }} />
  );
}

/** A length cell of the properties panel: empty clears it (LengthField with `blank`). */
export function LengthCell({ metres, onCommit, placeholder }: { metres: number | null | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  return <LengthField metres={metres} onCommit={onCommit} placeholder={placeholder} blank />;
}
