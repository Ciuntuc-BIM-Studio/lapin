/**
 * The properties panel's cells, with a spreadsheet's feel: a cell commits on
 * Enter (and moves down) or when it loses focus, Escape puts its value back,
 * the arrows move between cells, focusing one selects its text. Lengths are
 * typed in the project's unit (or with a unit: 240cm, 2.4m) and numbers may
 * be sums. LengthField and NumberField are the interface's one length and
 * number field, in the panel and outside it.
 *
 * Drag-to-Scrub (docs/plan-ux-game-like.md, P8), a Flow extra (Preferences →
 * General; off by default): drag horizontally on the unit badge (or Alt+drag
 * on a numeric field) to adjust smoothly (Shift = fine, Ctrl = coarse);
 * commits once on release as a single undo step; integer fields stay whole.
 */

import { useEffect, useState, type CSSProperties, type PointerEvent as ReactPointerEvent } from 'react';
import { formatLength, unitLabel, useStandards } from '../engine/unitsStore';
import { commitLength, commitNumber, NO_COMMIT, type CommitRules } from './fieldCommit';
import { usePrefs } from '../app/prefs';

export interface ScrubConfig {
  start: number;
  step: number;
  fine: number;
  coarse: number;
  min?: number;
  max?: number;
  /** Whole numbers only (a field's `integer` rule): every value it passes is rounded, a fine step is one. */
  integer?: boolean;
  format: (v: number) => string;
  commit: (v: number) => void;
}

/** Pure value calculation for horizontal drag-to-scrub (4 px per step). */
export function scrubValue(start: number, dx: number, cfg: Pick<ScrubConfig, 'step' | 'fine' | 'coarse' | 'min' | 'max' | 'integer'>, mods: { shift: boolean; ctrl: boolean }): number {
  let unit = mods.shift ? cfg.fine : mods.ctrl ? cfg.coarse : cfg.step;
  if (cfg.integer) unit = Math.max(1, Math.round(unit));
  const ticks = Math.trunc(dx / 4);
  let next = Math.round((start + ticks * unit) * 1e6) / 1e6;
  if (cfg.integer) next = Math.round(next);
  if (cfg.min != null && next < cfg.min) next = cfg.min;
  if (cfg.max != null && next > cfg.max) next = cfg.max;
  return next;
}

/** A cell: commits on blur or Enter, Escape restores; an onCommit that returns false (refused) puts the value back. */
export function Cell({ value, onCommit, suffix, placeholder, className, readOnly, title, style, testId, scrub }: {
  value: string; onCommit: (text: string) => boolean | void; suffix?: string; placeholder?: string; className?: string; readOnly?: boolean; title?: string;
  /** The input's own style (a width in a row of fields). */
  style?: CSSProperties;
  /** The input's data-field, for the guide's scenes and tests. */
  testId?: string;
  /** Optional horizontal drag-to-scrub handler for numeric/length fields. */
  scrub?: ScrubConfig;
}) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);

  const startScrub = (e: ReactPointerEvent) => {
    if (!scrub || readOnly || e.button !== 0) return;
    e.preventDefault();
    const x0 = e.clientX;
    let latest = scrub.start;
    let moved = false;
    const onMove = (ev: PointerEvent) => {
      const dx = ev.clientX - x0;
      if (!moved && Math.abs(dx) < 3) return;
      moved = true;
      latest = scrubValue(scrub.start, dx, scrub, { shift: ev.shiftKey, ctrl: ev.ctrlKey || ev.metaKey });
      setText(scrub.format(latest));
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onUp);
      if (moved && latest !== scrub.start) scrub.commit(latest);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onUp);
  };

  return (
    <span className="cell-wrap" style={style ? { width: 'auto' } : undefined}>
      <input className={`cell${className ? ` ${className}` : ''}`} value={text} placeholder={placeholder} readOnly={readOnly} title={title} style={style} data-field={testId}
        onPointerDown={(e) => { if (e.altKey && scrub) startScrub(e); }}
        onChange={(e) => setText(e.target.value)}
        onBlur={() => { if (text !== value && onCommit(text) === false) setText(value); }}
        // Enter blurs (which commits); not prevented, so the properties panel's keys still move down.
        onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape' && text !== value) { setText(value); e.preventDefault(); } }} />
      {suffix && (
        <span className={`cell-unit${scrub && !readOnly ? ' scrubbable' : ''}`}
          title={scrub && !readOnly ? 'Drag left/right to adjust · Shift: fine · Ctrl: coarse' : undefined}
          onPointerDown={scrub && !readOnly ? startScrub : undefined}>{suffix}</span>
      )}
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
 * empty commits null: the default). `min` and `max` are in metres; `noUnit`
 * leaves the unit's label off (the first of a pair, x before y).
 */
export function LengthField({ metres, onCommit, placeholder, className, readOnly, title, style, testId, noUnit, ...rules }: FieldProps & { metres: number | null | undefined; onCommit: (m: number | null) => void; noUnit?: boolean }) {
  const u = useStandards((s) => s.units);
  const shown = typeof metres === 'number' ? formatLength(metres, u) : '';
  const scrubOn = usePrefs((p) => p.scrubFields);
  const scrub: ScrubConfig | undefined = scrubOn && typeof metres === 'number' && !readOnly ? {
    start: metres, step: 0.01, fine: 0.001, coarse: 0.1, min: rules.min, max: rules.max,
    format: (m) => formatLength(m, u),
    commit: (m) => onCommit(m),
  } : undefined;
  return (
    <Cell value={shown} className={`num${className ? ` ${className}` : ''}`} suffix={noUnit ? undefined : unitLabel(u) || undefined} placeholder={placeholder} readOnly={readOnly} title={title} style={style} testId={testId} scrub={scrub}
      onCommit={(t) => { const v = commitLength(t, metres, rules); if (v !== NO_COMMIT) onCommit(v); return v !== NO_COMMIT; }} />
  );
}

/** The one number field: sums allowed, a decimal comma too; the same commit rules as LengthField. */
export function NumberField({ value, onCommit, suffix, placeholder, className, readOnly, title, style, testId, ...rules }: FieldProps & { value: number | null | undefined; onCommit: (n: number | null) => void; suffix?: string }) {
  const scrubOn = usePrefs((p) => p.scrubFields);
  const scrub: ScrubConfig | undefined = scrubOn && typeof value === 'number' && !readOnly ? {
    start: value, step: 1, fine: 0.1, coarse: 10, min: rules.min, max: rules.max, integer: rules.integer,
    format: (n) => String(n),
    commit: (n) => onCommit(n),
  } : undefined;
  return (
    <Cell value={typeof value === 'number' ? String(value) : ''} className={`num${className ? ` ${className}` : ''}`} suffix={suffix} placeholder={placeholder} readOnly={readOnly} title={title} style={style} testId={testId} scrub={scrub}
      onCommit={(t) => { const v = commitNumber(t, value, rules); if (v !== NO_COMMIT) onCommit(v); return v !== NO_COMMIT; }} />
  );
}

/** A length cell of the properties panel: empty clears it (LengthField with `blank`). */
export function LengthCell({ metres, onCommit, placeholder }: { metres: number | null | undefined; onCommit: (m: number | null) => void; placeholder?: string }) {
  return <LengthField metres={metres} onCommit={onCommit} placeholder={placeholder} blank />;
}

/** A number or length the engine describes (`min`/`max` honoured, angles in degrees): LengthField or NumberField by its kind. */
export function DescriptorField({ f, value, onChange }: { f: { kind: string; min?: number | null; max?: number | null }; value: unknown; onChange: (v: number) => void }) {
  const n = typeof value === 'number' ? value : null;
  const min = f.min ?? undefined, max = f.max ?? undefined;
  return f.kind === 'length'
    ? <LengthField metres={n} min={min} max={max} onCommit={(m) => { if (m != null) onChange(m); }} />
    : <NumberField value={n} min={min} max={max} suffix={f.kind === 'angle' ? '°' : undefined} onCommit={(v) => { if (v != null) onChange(v); }} />;
}

/** A label and a length field (metres), one commit on blur or Enter. */
export function LabeledLength({ label, metres, onCommit, className, ...rules }: FieldProps & { label: string; metres: number | null | undefined; onCommit: (m: number) => void }) {
  return <label className={`field-line${className ? ` ${className}` : ''}`}><span className="eyebrow">{label}</span><LengthField metres={metres} {...rules} onCommit={(m) => { if (m != null) onCommit(m); }} /></label>;
}

/** A label and a number field, one commit on blur or Enter. */
export function LabeledNumber({ label, value, onCommit, className, suffix, ...rules }: FieldProps & { label: string; value: number | null | undefined; onCommit: (n: number) => void; suffix?: string }) {
  return <label className={`field-line${className ? ` ${className}` : ''}`}><span className="eyebrow">{label}</span><NumberField value={value} suffix={suffix} {...rules} onCommit={(n) => { if (n != null) onCommit(n); }} /></label>;
}
