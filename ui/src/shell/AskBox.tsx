/**
 * A value asked for beside the cursor (a push/pull's distance, a fillet's
 * radius, a count): Studio's face tools work in 3D, where there is no command
 * line. Enter takes it, Esc drops it. Lengths are typed in the project's unit.
 */

import { useEffect, useRef, useState } from 'react';
import { create } from 'zustand';
import { parseLength, formatLength, unitLabel } from '../engine/unitsStore';

interface Field { key: string; label: string; value: string; length?: boolean; choices?: { value: string; label: string }[] }
interface Asking { title: string; fields: Field[]; x: number; y: number; resolve: (v: Record<string, string | number> | null) => void }

const useAsk = create<{ asking: Asking | null }>(() => ({ asking: null }));

/** Where the pointer was last: the box opens there. */
const last = { x: 400, y: 300 };
if (typeof window !== 'undefined') {
  last.x = window.innerWidth / 2; last.y = window.innerHeight / 2;
  window.addEventListener('pointermove', (e) => { last.x = e.clientX; last.y = e.clientY; }, { capture: true, passive: true });
}

/** One length (metres in and out); null when dropped. */
export async function askLength(title: string, label: string, initial: number): Promise<number | null> {
  const r = await ask(title, [{ key: 'v', label, value: formatLength(initial), length: true }]);
  return r ? (r.v as number) : null;
}

/** Several values at once: lengths parsed in the project's unit, numbers as numbers, choices as they are. */
export function ask(title: string, fields: Field[]): Promise<Record<string, string | number> | null> {
  useAsk.getState().asking?.resolve(null);
  return new Promise((resolve) => useAsk.setState({ asking: { title, fields, x: last.x, y: last.y, resolve } }));
}

export function lengthField(key: string, label: string, metres: number): Field { return { key, label, value: formatLength(metres), length: true }; }
export function numberField(key: string, label: string, n: number): Field { return { key, label, value: String(n) }; }
export function choiceField(key: string, label: string, value: string, choices: { value: string; label: string }[]): Field { return { key, label, value, choices }; }

/** The box itself, over everything, near the pointer (mounted by the module's background). */
export function AskBox() {
  const asking = useAsk((s) => s.asking);
  const [values, setValues] = useState<Record<string, string>>({});
  const first = useRef<HTMLInputElement | null>(null);
  useEffect(() => {
    if (!asking) return;
    setValues(Object.fromEntries(asking.fields.map((f) => [f.key, f.value])));
    setTimeout(() => { first.current?.focus(); first.current?.select(); }, 0);
  }, [asking]);
  if (!asking) return null;
  const close = (v: Record<string, string | number> | null) => { useAsk.setState({ asking: null }); asking.resolve(v); };
  const done = () => {
    const out: Record<string, string | number> = {};
    for (const f of asking.fields) {
      const t = (values[f.key] ?? '').trim();
      if (f.choices) { out[f.key] = t; continue; }
      const n = f.length ? parseLength(t) : Number(t.replace(',', '.'));
      if (n == null || !Number.isFinite(n)) { first.current?.focus(); return; }
      out[f.key] = n;
    }
    close(out);
  };
  const left = Math.min(asking.x + 14, window.innerWidth - 280);
  const top = Math.min(asking.y + 14, window.innerHeight - 40 - asking.fields.length * 30);
  return (
    <div className="ask-box" style={{ left, top }} role="dialog" aria-label={asking.title}
      onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') done(); if (e.key === 'Escape') close(null); }}>
      <div className="ask-title">{asking.title}</div>
      {asking.fields.map((f, i) => (
        <label key={f.key} className="ask-row">
          <span>{f.label}</span>
          {f.choices
            ? <select value={values[f.key] ?? f.value} onChange={(e) => setValues({ ...values, [f.key]: e.target.value })}>{f.choices.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}</select>
            : <input ref={i === 0 ? first : undefined} className="num" value={values[f.key] ?? ''} onChange={(e) => setValues({ ...values, [f.key]: e.target.value })} />}
          {f.length && <span className="muted">{unitLabel()}</span>}
        </label>
      ))}
      <div className="ask-actions">
        <button type="button" className="btn" onClick={() => close(null)}>Cancel</button>
        <button type="button" className="btn primary" onClick={done}>OK</button>
      </div>
    </div>
  );
}
