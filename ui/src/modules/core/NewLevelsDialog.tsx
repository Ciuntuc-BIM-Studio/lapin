/**
 * New levels… (issue #83): several storeys at once, one on another from a base level, from their heights typed as the
 * Axis grid's spans are (core/spans: `3*3000 2800`), named by a pattern ({n} the number). One undo step
 * (storey.createMany); the levels already there are left as they are.
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { num, storeys } from '../../engine/model';
import { LengthField, NumberField } from '../../shell/cells';
import { lengthText, useStandards } from '../../engine/unitsStore';
import { level } from './format';
import { parseSpans } from './spans';
import { levelPlan, namesApart } from './newLevels';
import { HEIGHTS_HINT, SpansHint } from './SpansHint';

export function NewLevelsDialog() {
  const units = useStandards((s) => s.units);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const list = storeys({ nodes, relations });
  const top = list.length ? Math.max(...list.map((s) => num(s, 'elevation') + num(s, 'height', 3))) : 0;
  const metric = units !== 'ft-in' && units !== 'in';
  const [heights, setHeights] = useState(metric ? (units === 'mm' ? '3*3000' : units === 'cm' ? '3*300' : '3*3') : "3*10'");
  const [base, setBase] = useState<number>(Math.round(top * 1e6) / 1e6);
  const [pattern, setPattern] = useState('Level {n}');
  const [start, setStart] = useState(list.length);
  const read = parseSpans(heights);
  const plan = read.ok ? levelPlan(base, read.spans, pattern, start) : [];
  const apart = namesApart(pattern, plan.length);
  const ok = read.ok && plan.length > 0 && apart;
  const close = () => useUi.setState({ dialog: null });
  const make = async () => {
    if (!ok) return;
    const r = await execute('storey.createMany', { heights: read.spans, elevation: base, name: pattern, start });
    if (r) { notify('The levels are made.'); close(); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="New levels">
      <div className="dialog" data-new-levels>
        <header className="dialog-head">
          <span className="panel-title">New levels</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body row" style={{ alignItems: 'flex-start', gap: 16 }}>
          <dl className="fields" style={{ flex: 1 }}>
            <dt title="The level the first new storey stands on; by default the top of the highest storey">Base level</dt>
            <dd><LengthField metres={base} onCommit={(v) => { if (v != null) setBase(v); }} /></dd>
            <dt title="Storey heights, bottom up: 3000 3000 2800, or 3*3000 2800">Storey heights</dt>
            <dd><input className="cell num" style={{ minWidth: 200 }} value={heights} data-levels-heights onChange={(e) => setHeights(e.target.value)} aria-invalid={!read.ok} /><SpansHint text={HEIGHTS_HINT} /></dd>
            <dt title="Each storey's name: {n} is its number">Names</dt>
            <dd><input className="cell" style={{ minWidth: 200 }} value={pattern} data-no-i18n data-levels-pattern onChange={(e) => setPattern(e.target.value)} aria-invalid={!apart} /></dd>
            <dt title="The number {n} of the first new storey; each next one counts up">First number</dt>
            <dd><NumberField value={start} onCommit={(v) => { if (v != null) setStart(Math.round(v)); }} /></dd>
            <dt>Total height</dt><dd className="num muted">{read.ok && read.spans.length ? lengthText(read.spans.reduce((a, b) => a + b, 0)) : '—'}</dd>
          </dl>
          <ul className="muted small" data-no-i18n data-levels-preview style={{ minWidth: 160, maxHeight: 240, overflow: 'auto', margin: 0, paddingLeft: 16 }}>
            {plan.slice().reverse().map((l, i) => <li key={i}>{l.name} · {level(l.elevation)}</li>)}
          </ul>
        </div>
        <footer className="dialog-foot">
          {!read.ok && <span className="error-text" data-levels-error>{read.error} <code data-no-i18n>{read.token}</code></span>}
          {read.ok && !apart && <span className="error-text">Name several storeys with {'{n}'} in the name.</span>}
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-levels-make disabled={!ok} onClick={() => void make()}>Make the levels</button>
        </footer>
      </div>
    </div>
  );
}
