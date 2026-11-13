/**
 * A user parameter's Value / Formula switch (docs/plan-user-extensibility.md §5.3): a formula input with a live try (the value it
 * gives, or why it does not read), the computed value shown read-only, and a help of the names and functions a formula may use.
 * The formula is kept as written; the engine works the value out on every ask, from the element's and its type's parameters and
 * numbers (and its quantities).
 */

import { useEffect, useState, type ReactNode } from 'react';
import { Sigma } from 'lucide-react';
import { execute, query, useSession } from '../engine/session';
import { formatLength } from '../engine/unitsStore';
import { notify } from '../app/store';
import { FORMULA_FUNCTIONS, FORMULA_QUANTITIES, canFormula, formulaArgs, resultText, toggled, usableNames, valueArgs, worthTrying, type FormulaState, initialState } from './formulaModel';
import type { Param } from './params';

export interface Evaluated { name: string; pset?: string | null; formula: string; value: number | null; error: string | null }

/** What the engine works out for the formula parameters of one element or type (kept fresh as the model changes). */
export function useEvaluated(id: string, any: boolean): Evaluated[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<Evaluated[]>([]);
  useEffect(() => {
    if (!any) { setList([]); return; }
    let live = true;
    void query<{ params: Evaluated[] }>('params.evaluate', { id }).then((r) => { if (live) setList(r?.params ?? []); });
    return () => { live = false; };
  }, [id, any, version]);
  return list;
}

const same = (a: Pick<Param, 'name' | 'pset'>, b: Evaluated) => a.name === b.name && (a.pset ?? '') === (b.pset ?? '');

export function ParamValue({ p, id, ids, evaluated, params, attrs, children }: {
  p: Param & { formula?: string | null }; id: string; ids: () => string[]; evaluated: Evaluated[]; params: Param[]; attrs: Record<string, unknown>; children: ReactNode;
}) {
  const [state, setState] = useState<FormulaState>(() => initialState(p));
  const [tried, setTried] = useState<{ value: number | null; error: string | null } | null>(null);
  // A change from elsewhere (undo, another selection) is what the row shows.
  useEffect(() => { setState(initialState(p)); setTried(null); }, [p.formula, p.name]);
  const mine = evaluated.find((e) => same(p, e));
  // The live try of what is typed (not yet saved).
  useEffect(() => {
    if (state.mode !== 'formula' || !worthTrying(state.text, p.formula)) { setTried(null); return; }
    let live = true;
    const t = setTimeout(() => { void query<{ value: number | null; error: string | null }>('params.tryFormula', { id, formula: state.text }).then((r) => { if (live) setTried(r); }); }, 200);
    return () => { live = false; clearTimeout(t); };
  }, [state.mode, state.text, id, p.formula]);

  if (!canFormula(p.kind)) return <>{children}</>;
  const length = (m: number) => `${formatLength(m)}`;
  const toggle = () => {
    // Back to a value: the formula's result is kept as the value.
    if (state.mode === 'formula' && p.formula) { void execute('element.setParam', valueArgs(ids(), p, mine?.value)); }
    setState(toggled(state, p));
  };
  const commit = () => {
    const text = state.text.trim();
    if (!text || text === (p.formula ?? '')) return;
    void execute('element.setParam', formulaArgs(ids(), p, text)).then((r) => { if (r == null && useSession.getState().error) notify(useSession.getState().error!); });
  };
  const shown = tried ?? (mine ? { value: mine.value, error: mine.error } : null);
  const names = usableNames(params, attrs, p.name);
  return (
    <span className="param-value" data-param-mode={state.mode}>
      <button type="button" className={`icon mini param-formula-switch${state.mode === 'formula' ? ' on' : ''}`} data-param-formula-switch={p.name} aria-pressed={state.mode === 'formula'}
        title={state.mode === 'formula' ? 'A formula: press for a value' : 'A value: press to work it out by a formula'} onClick={toggle}><Sigma size={11} /></button>
      {state.mode === 'value' ? children : (
        <span className="param-formula">
          <input className="mono" data-param-formula={p.name} value={state.text} placeholder="Height * 2" spellCheck={false}
            onChange={(e) => setState({ ...state, text: e.target.value })} onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') commit(); }} />
          <span className={`small param-result${shown?.error ? ' error-text' : ' muted'}`} data-param-result={p.name} data-no-i18n>
            {shown?.error ?? (shown?.value != null ? `= ${resultText(shown.value, p.kind, length)}` : '')}
          </span>
          <details className="small param-formula-help">
            <summary>Names and functions</summary>
            <p>{names.length ? 'Names you can use: ' : 'Names you can use: the element\'s parameters and numbers, '}<span data-no-i18n>{names.join(', ')}</span></p>
            <p>Quantities, when the element has them: <span data-no-i18n>{FORMULA_QUANTITIES.join(', ')}</span></p>
            <p>Functions: <span data-no-i18n>{FORMULA_FUNCTIONS.join(', ')}</span></p>
            <p>Decimals with a dot; arguments separated by , or ;. A name with spaces may be written without them (Fire rating or FireRating).</p>
          </details>
        </span>
      )}
    </span>
  );
}
