/**
 * A domain's notation in a settings dialog (decision 856): the preset (indicative), each text as a template with its tokens,
 * a symbol with a picker beside free text, an example of what it writes, and the way back to Lapin's own. Generic: any module
 * that registers a notation domain shows it with this block.
 */

import { useEffect, useState } from 'react';
import { groupKeys, setNotation, SYMBOLS, tokenHint, useNotation, type NotationKey } from './notation';

/** A text applied when left or on Enter (a template half typed is not sent); Esc puts it back. */
function TemplateInput({ value, placeholder, onCommit, testId }: { value: string; placeholder: string; onCommit: (v: string) => void; testId: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return <input className="cell" type="text" value={text} placeholder={placeholder} spellCheck={false} data-no-i18n data-field={testId} style={{ width: 150 }}
    onChange={(e) => setText(e.target.value)} onBlur={() => { if (text !== value) onCommit(text); }}
    onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') setText(value); e.stopPropagation(); }} />;
}

function KeyRow({ domain, k }: { domain: string; k: NotationKey }) {
  const commit = (text: string) => void setNotation(domain, { key: k.key, text });
  const testId = `notation-${domain}-${k.key}`;
  return (
    <label className="field-line" data-notation-key={k.key}>
      <span className="eyebrow">{k.label}</span>
      {k.kind === 'symbol' && (
        <select value={SYMBOLS.includes(k.value) ? k.value : ''} data-field={`${testId}-pick`} onChange={(e) => { if (e.target.value) commit(e.target.value); }}>
          <option value="">Other…</option>
          {SYMBOLS.map((s) => <option key={s} value={s} data-no-i18n>{s}</option>)}
        </select>
      )}
      <TemplateInput value={k.value} placeholder={k.default} onCommit={commit} testId={testId} />
      {k.example && k.example !== k.value && <span className="muted small num" data-no-i18n> {k.example}</span>}
      {k.own && <button type="button" className="btn small" title="Back to the preset's or Lapin's" onClick={() => commit('')}>Reset</button>}
    </label>
  );
}

export function NotationFields({ domain }: { domain: string }) {
  const d = useNotation(domain);
  if (!d) return null;
  const { drawing, schedule } = groupKeys(d.keys);
  const hints = d.keys.filter((k) => k.kind === 'template' && k.tokens.length > 0).map((k) => `${k.label}: ${tokenHint(k)}`).join(' · ');
  return (
    <div data-field={`notation-${domain}`}>
      <p className="eyebrow">Notation</p>
      <label className="field-line"><span className="eyebrow">Preset</span>
        <select value={d.preset ?? ''} data-field={`notation-${domain}-preset`} onChange={(e) => void setNotation(domain, { preset: e.target.value || null })}>
          <option value="">Lapin's default</option>
          {d.presets.map((p) => <option key={p.id} value={p.id} title={p.source}>{p.label}</option>)}
        </select>
      </label>
      <p className="muted small">Symbols, prefixes and spacing differ from one country to another. The presets are indicative: check them against the standard you work to. A text you type goes over the preset; empty, it goes back.</p>
      {drawing.map((k) => <KeyRow key={k.key} domain={domain} k={k} />)}
      <p className="muted small" data-no-i18n>{hints}</p>
      <p className="eyebrow">Schedule</p>
      {schedule.map((k) => <KeyRow key={k.key} domain={domain} k={k} />)}
      <p className="muted small">{'A formula works in any text: {= round(d / 25.4; 2)} writes the diameter in inches.'}</p>
      <div className="row small"><button type="button" className="btn" data-action={`notation-${domain}-reset`} onClick={() => void setNotation(domain, { reset: true })}>Back to Lapin's notation</button></div>
    </div>
  );
}
