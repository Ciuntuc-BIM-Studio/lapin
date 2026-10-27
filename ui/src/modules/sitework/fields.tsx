/** The small fields the site works' dialogs and sections share: lengths in mm, lists of lengths, numbers. */

import { useEffect, useState } from 'react';

const mm = (v: number) => String(Math.round(v * 1000));

/** A length in mm, committed on Enter or blur; empty commits null when `blank` says what empty means. */
export function Mm({ value, onCommit, min, blank, unit = true }: { value: number | null | undefined; onCommit: (m: number | null) => void; min?: number; blank?: string; unit?: boolean }) {
  const shown = typeof value === 'number' ? mm(value) : '';
  const [text, setText] = useState(shown);
  useEffect(() => setText(shown), [shown]);
  const done = () => {
    if (text.trim() === '') { if (blank && shown !== '') onCommit(null); else setText(shown); return; }
    const n = Number(text.replace(',', '.'));
    if (Number.isFinite(n) && text !== shown) onCommit(Math.max(min ?? -Infinity, n) / 1000); else setText(shown);
  };
  return <><input className="num" value={text} placeholder={blank} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />{unit && <> <span className="muted">mm</span></>}</>;
}

/** Lengths in mm separated by spaces (a catalogue: 3070 2570 2070). */
export function MmList({ value, onCommit }: { value: number[]; onCommit: (v: number[]) => void }) {
  const shown = value.map(mm).join(' ');
  const [text, setText] = useState(shown);
  useEffect(() => setText(shown), [shown]);
  const done = () => {
    const l = text.split(/[ ,;]+/).map((x) => Number(x)).filter((x) => Number.isFinite(x) && x > 0).map((x) => x / 1000);
    if (l.length > 0) onCommit(l); else setText(shown);
  };
  return <input value={text} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} title="Lengths in mm, separated by spaces" />;
}

/** A plain number kept as text while typing. */
export function Num({ value, onCommit, step = 1, min }: { value: number; onCommit: (v: number) => void; step?: number; min?: number }) {
  return <input className="num" type="number" step={step} min={min} value={value} onChange={(e) => { const n = Number(e.target.value); if (Number.isFinite(n)) onCommit(min != null ? Math.max(min, n) : n); }} />;
}
