/**
 * A value asked for beside the cursor (a push/pull's distance, a fillet's
 * radius, a count): Studio's face tools work in 3D, where there is no command
 * line. Enter takes it, Esc drops it. Lengths are typed in the project's unit.
 * A field may show only for some answers of another (`show`: the Array's
 * method, forum 90); what is wrong is said in the box, by the field's name,
 * and keeps it open (`check`). The box moves by its title, as dialogs do
 * (shell/dialogDrag.ts).
 */

import { useEffect, useRef, useState } from 'react';
import { create } from 'zustand';
import { parseLength, formatLength, unitLabel } from '../engine/unitsStore';

export interface Field {
  key: string; label: string; value: string; length?: boolean; text?: boolean; choices?: { value: string; label: string }[];
  /** Shown only when this says so of the answers as typed (each field's text, a choice's value). */
  show?: (values: Record<string, string>) => boolean;
}
/** What is wrong with the answers: the field to change and why; null when nothing is. */
export type AskProblem = { key: string; message: string } | null;
export type AskCheck = (answers: Record<string, string | number>) => AskProblem;
interface Asking { title: string; fields: Field[]; x: number; y: number; check?: AskCheck; n: number; resolve: (v: Record<string, string | number> | null) => void }

const useAsk = create<{ asking: Asking | null }>(() => ({ asking: null }));
let asked = 0;

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

/**
 * Several values at once: lengths parsed in the project's unit, numbers as numbers, texts and choices as they are. A field
 * hidden by its `show` is answered too when it reads, else left out; `check` keeps the box open on a problem, said by the field.
 */
export function ask(title: string, fields: Field[], opts: { check?: AskCheck } = {}): Promise<Record<string, string | number> | null> {
  useAsk.getState().asking?.resolve(null);
  return new Promise((resolve) => useAsk.setState({ asking: { title, fields, x: last.x, y: last.y, check: opts.check, n: ++asked, resolve } }));
}

export function lengthField(key: string, label: string, metres: number): Field { return { key, label, value: formatLength(metres), length: true }; }
export function numberField(key: string, label: string, n: number): Field { return { key, label, value: String(n) }; }
export function textField(key: string, label: string, value: string): Field { return { key, label, value, text: true }; }
export function choiceField(key: string, label: string, value: string, choices: { value: string; label: string }[]): Field { return { key, label, value, choices }; }

/** The fields shown for the answers as typed. */
export const shownFields = (fields: readonly Field[], values: Record<string, string>): Field[] => fields.filter((f) => !f.show || f.show(values));

/**
 * The answers read: each field's text parsed (a length in the project's unit, a number), a choice or a text as it is. A field
 * shown that does not read is the problem; one hidden that does not read is left out.
 */
export function readAnswers(fields: readonly Field[], values: Record<string, string>, parse: (t: string) => number | null = parseLength): { out: Record<string, string | number>; problem: AskProblem } {
  const out: Record<string, string | number> = {};
  const shown = new Set(shownFields(fields, values).map((f) => f.key));
  for (const f of fields) {
    const t = (values[f.key] ?? f.value).trim();
    if (f.choices || f.text) { out[f.key] = t; continue; }
    const n = f.length ? parse(t) : Number(t.replace(',', '.'));
    if (n == null || !Number.isFinite(n)) {
      if (shown.has(f.key)) return { out, problem: { key: f.key, message: f.length ? 'Not a length.' : 'Not a number.' } };
      continue;
    }
    out[f.key] = n;
  }
  return { out, problem: null };
}

/** The box itself, over everything, near the pointer (mounted by the module's background). */
export function AskBox() {
  const asking = useAsk((s) => s.asking);
  const [values, setValues] = useState<Record<string, string>>({});
  const [problem, setProblem] = useState<AskProblem>(null);
  const box = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!asking) return;
    setValues(Object.fromEntries(asking.fields.map((f) => [f.key, f.value])));
    setProblem(null);
    // The first field takes the keys: a text in it selected, ready to be typed over.
    setTimeout(() => {
      const first = box.current?.querySelector<HTMLInputElement | HTMLSelectElement>('input, select');
      first?.focus();
      if (first instanceof HTMLInputElement) first.select();
    }, 0);
  }, [asking]);
  if (!asking) return null;
  const now: Record<string, string> = { ...Object.fromEntries(asking.fields.map((f) => [f.key, f.value])), ...values };
  const shown = shownFields(asking.fields, now);
  const close = (v: Record<string, string | number> | null) => { useAsk.setState({ asking: null }); asking.resolve(v); };
  const wrong = (p: NonNullable<AskProblem>) => {
    setProblem(p);
    setTimeout(() => {
      const el = box.current?.querySelector<HTMLInputElement | HTMLSelectElement>(`[data-field="${p.key}"]`);
      el?.focus();
      if (el instanceof HTMLInputElement) el.select();
    }, 0);
  };
  const done = () => {
    const { out, problem: unread } = readAnswers(asking.fields, now);
    if (unread) { wrong(unread); return; }
    const p = asking.check?.(out) ?? null;
    if (p) { wrong(p); return; }
    close(out);
  };
  const set = (key: string, v: string) => { setValues({ ...now, [key]: v }); if (problem?.key === key) setProblem(null); };
  const left = Math.max(4, Math.min(asking.x + 14, window.innerWidth - 300));
  const top = Math.max(4, Math.min(asking.y + 14, window.innerHeight - 70 - shown.length * 30));
  const labelOf = (key: string) => asking.fields.find((f) => f.key === key)?.label ?? key;
  return (
    // A new box for each question (its key), so one moved before opens at the pointer again.
    <div key={asking.n} ref={box} className="ask-box" style={{ left, top }} role="dialog" aria-label={asking.title}
      onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') done(); if (e.key === 'Escape') close(null); }}>
      <div className="ask-title" title="Drag to move">{asking.title}</div>
      {shown.map((f) => (
        <label key={f.key} className={`ask-row${problem?.key === f.key ? ' wrong' : ''}`}>
          <span>{f.label}</span>
          {f.choices
            ? <select data-field={f.key} value={now[f.key]} onChange={(e) => set(f.key, e.target.value)}>{f.choices.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}</select>
            : <input data-field={f.key} className={f.text ? undefined : 'num'} value={now[f.key] ?? ''} onChange={(e) => set(f.key, e.target.value)} />}
          {f.length && <span className="muted">{unitLabel()}</span>}
        </label>
      ))}
      {problem && (
        <p className="ask-problem" role="alert" data-testid="ask-problem">
          <b>{labelOf(problem.key)}</b> <span>{problem.message}</span>
        </p>
      )}
      <div className="ask-actions">
        <button type="button" className="btn" onClick={() => close(null)}>Cancel</button>
        <button type="button" className="btn primary" onClick={done}>OK</button>
      </div>
    </div>
  );
}
