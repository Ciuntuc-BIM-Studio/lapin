/**
 * Annotate → Renumber axes… (forum #75): a grid's axes renumbered in order across them, the selected ones or all of a
 * direction (those running up the plan numbered left to right, those across it bottom to top), in numbers or letters
 * (I and O left out) from a start, after an optional prefix. The order may run the other way (right to left, top to
 * bottom) and the labels count down (issue #64). Dimensions on the axes follow. One undo step.
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { renumberedSelection, runUpThePlan, selectedAxes } from './gridSpec';
import { axesOf } from '../../shell/gridAxes';

type Which = 'selected' | 'x' | 'y';

export function RenumberAxesDialog() {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const chosen = selectedAxes(selected);
  const grid = chosen ? nodes.get(chosen.gridId) : [...nodes.values()].find((n) => n.type === 'IfcGrid');
  const [which, setWhich] = useState<Which>(chosen && chosen.tags.length > 1 ? 'selected' : 'x');
  const [scheme, setScheme] = useState<'numbers' | 'letters'>('numbers');
  const [start, setStart] = useState('1');
  const [prefix, setPrefix] = useState('');
  const [skipIO, setSkipIO] = useState(true);
  const [reverse, setReverse] = useState(false);
  const [descending, setDescending] = useState(false);
  // The family renumbered: the selected axes' own, else the direction chosen.
  const upThePlan = which === 'selected' && chosen ? runUpThePlan(axesOf(grid?.props.axes), chosen.tags) ?? true : which === 'x';
  const first = (v: 'numbers' | 'letters', down: boolean) => (down ? '' : v === 'numbers' ? '1' : 'A');
  const close = () => useUi.setState({ dialog: null });
  // A direction suggests its usual scheme: numbers left to right, letters bottom to top.
  const pick = (w: Which) => {
    setWhich(w);
    if (w === 'x') { setScheme('numbers'); setStart(first('numbers', descending)); }
    if (w === 'y') { setScheme('letters'); setStart(first('letters', descending)); }
  };
  const go = async () => {
    if (!grid) return;
    // The default order sends neither flag, as before.
    const args = { gridId: grid.id, scheme, start, prefix, skipIO, ...(reverse ? { reverse } : {}), ...(descending ? { descending } : {}), ...(which === 'selected' && chosen ? { tags: chosen.tags } : { direction: which }) };
    const r = await execute('drafting.renumberAxes', args) as { tags?: Record<string, string>; changed?: number } | null;
    if (!r) return;
    const s = useUi.getState();
    if (r.tags) useUi.setState({ selected: renumberedSelection(s.selected, grid.id, r.tags), selection: s.selection ? renumberedSelection([s.selection], grid.id, r.tags)[0] : s.selection });
    notify(r.changed ? `${r.changed} axes renumbered.` : 'The axes already have those labels.');
    close();
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Renumber axes">
      <div className="dialog" data-renumber-axes>
        <header className="dialog-head">
          <span className="panel-title">Renumber axes</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          {!grid ? <p className="muted">The project has no grid yet: draw axes or make a grid first.</p> : (
            <dl className="fields">
              <dt title="The axes renumbered, in order across them">Axes</dt>
              <dd><select data-renumber-which value={which} onChange={(e) => pick(e.target.value as Which)}>
                {chosen && <option value="selected">{`the selected axes (${chosen.tags.length})`}</option>}
                <option value="x">running up the plan</option>
                <option value="y">running across the plan</option>
              </select></dd>
              <dt title="Which way the labels run across the axes">Order</dt>
              <dd><select data-renumber-order value={reverse ? 'reverse' : 'forward'} onChange={(e) => setReverse(e.target.value === 'reverse')}>
                <option value="forward">{upThePlan ? 'left to right' : 'bottom to top'}</option>
                <option value="reverse">{upThePlan ? 'right to left' : 'top to bottom'}</option>
              </select></dd>
              <dt>Labels</dt>
              <dd><select data-renumber-scheme value={scheme} onChange={(e) => { const v = e.target.value as 'numbers' | 'letters'; setScheme(v); setStart(first(v, descending)); }}>
                <option value="numbers">numbers (1, 2, 3…)</option>
                <option value="letters">letters (A, B, C…)</option>
              </select></dd>
              <dt title="Up from the start, or down from it">Counting</dt>
              <dd><select data-renumber-counting value={descending ? 'descending' : 'ascending'} onChange={(e) => { const down = e.target.value === 'descending'; setDescending(down); setStart(first(scheme, down)); }}>
                <option value="ascending">ascending</option>
                <option value="descending">descending</option>
              </select></dd>
              {descending
                ? <dt title="The first axis's label, counted down from: 10, E…; empty: the last one is 1 or A">Start</dt>
                : <dt title="The first axis's label: 1, 01, 101, A, AA…">Start</dt>}
              <dd><input className="cell" data-renumber-start data-no-i18n value={start} placeholder={descending ? (scheme === 'numbers' ? '…, 2, 1' : '…, B, A') : undefined} onChange={(e) => setStart(e.target.value)} /></dd>
              <dt title="Written before every label: S1, S2… or A-01">Prefix</dt>
              <dd><input className="cell" data-renumber-prefix data-no-i18n value={prefix} onChange={(e) => setPrefix(e.target.value)} /></dd>
              {scheme === 'letters' && <>
                <dt />
                <dd><label title="I and O read as 1 and 0 on drawings"><input type="checkbox" checked={skipIO} onChange={(e) => setSkipIO(e.target.checked)} /> leave out I and O</label></dd>
              </>}
            </dl>
          )}
          <p className="muted small">Dimensions on the axes keep measuring them.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-renumber-go disabled={!grid || (!descending && !start.trim())} onClick={() => void go()}>Renumber</button>
        </footer>
      </div>
    </div>
  );
}
