/**
 * The choice an exchange offers, the same for an import and an export, IFC or Fragments (docs/fragments.md,
 * Exchange): the elements by discipline, each discipline by kind with how many there are, and the storeys with
 * how many elements each carries. What is unticked is kept as `off` sets; `choiceOf` turns them into the engine's.
 * `kindsAfter` and `storeysAfter` go under each column (an IFC file's property sets, its 2D, the import's rules).
 */

import { useState, type ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { Survey, SurveyGroup } from '../../engine/ifcImport';

/** A checkbox showing all, none or some of its rows. */
function Tri({ on, some, onChange, label }: { on: boolean; some: boolean; onChange: (on: boolean) => void; label: string }) {
  return (
    <input type="checkbox" aria-label={label} checked={on} ref={(el) => { if (el) el.indeterminate = some && !on; }}
      onChange={(e) => onChange(e.target.checked)} />
  );
}

const toggle = (set: ReadonlySet<string>, keys: string[], on: boolean) => {
  const next = new Set(set);
  for (const k of keys) { if (on) next.delete(k); else next.add(k); }
  return next;
};

/** Every storey key of a survey ('' for the elements on none). */
export const storeyKeysOf = (survey: Survey) => [...survey.storeys.map((s) => s.id), ...(survey.noStorey ? [''] : [])];

/** Whether nothing at all is chosen. */
export const nothingChosen = (survey: Survey, offKinds: ReadonlySet<string>, offStoreys: ReadonlySet<string>) =>
  offStoreys.size === storeyKeysOf(survey).length || survey.groups.every((g) => g.kinds.every((k) => offKinds.has(k.key)));

export function SurveyChoice({ survey, whatLabel, offKinds, setOffKinds, offStoreys, setOffStoreys, kindsAfter, storeysAfter }: {
  survey: Survey; whatLabel: string; kindsAfter?: ReactNode; storeysAfter?: ReactNode;
  offKinds: ReadonlySet<string>; setOffKinds: (s: Set<string>) => void;
  offStoreys: ReadonlySet<string>; setOffStoreys: (s: Set<string>) => void;
}) {
  const [open, setOpen] = useState<Set<string>>(new Set());
  const groupState = (g: SurveyGroup) => {
    const off = g.kinds.filter((k) => offKinds.has(k.key)).length;
    return { on: off === 0, some: off < g.kinds.length };
  };
  const storeyKeys = storeyKeysOf(survey);
  return (
    <>
      <section>
        <h4 className="eyebrow">{whatLabel}</h4>
        <div className="row small">
          <button type="button" className="btn small" onClick={() => setOffKinds(new Set())}>All</button>
          <button type="button" className="btn small" onClick={() => setOffKinds(new Set(survey.groups.flatMap((g) => g.kinds.map((k) => k.key))))}>None</button>
        </div>
        <ul className="import-tree">
          {survey.groups.map((g) => {
            const st = groupState(g);
            const expanded = open.has(g.discipline);
            return (
              <li key={g.discipline} data-discipline={g.discipline}>
                <div className="import-row">
                  <button type="button" className="icon mini" title={expanded ? 'Fold' : 'Show its kinds'} onClick={() => setOpen(toggle(open, [g.discipline], expanded))}>
                    {expanded ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
                  </button>
                  <label>
                    <Tri on={st.on} some={st.some} label={g.label} onChange={(on) => setOffKinds(toggle(offKinds, g.kinds.map((k) => k.key), on))} />
                    <strong>{g.label}</strong>
                  </label>
                  <span className="grow" /><span className="num muted">{g.count}</span>
                </div>
                {expanded && (
                  <ul>
                    {g.kinds.map((k) => (
                      <li key={k.key} className="import-row">
                        <label>
                          <input type="checkbox" checked={!offKinds.has(k.key)} onChange={(e) => setOffKinds(toggle(offKinds, [k.key], e.target.checked))} />
                          <span data-no-i18n={k.category ? true : undefined}>{k.label}</span>
                        </label>
                        <span className="grow" /><span className="num muted">{k.count}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ul>
        {kindsAfter}
      </section>
      <section>
        <h4 className="eyebrow">Storeys</h4>
        <div className="row small">
          <button type="button" className="btn small" onClick={() => setOffStoreys(new Set())}>All</button>
          <button type="button" className="btn small" onClick={() => setOffStoreys(new Set(storeyKeys))}>None</button>
        </div>
        <ul className="import-tree">
          {[...survey.storeys].reverse().map((s) => (
            <li key={s.id} className="import-row">
              <label>
                <input type="checkbox" checked={!offStoreys.has(s.id)} onChange={(e) => setOffStoreys(toggle(offStoreys, [s.id], e.target.checked))} />
                <span data-no-i18n>{s.name}</span> <span className="muted small">{s.elevation >= 0 ? '+' : ''}{s.elevation.toFixed(2)}</span>
              </label>
              <span className="grow" /><span className="num muted">{s.count}</span>
            </li>
          ))}
          {survey.noStorey > 0 && (
            <li className="import-row">
              <label>
                <input type="checkbox" checked={!offStoreys.has('')} onChange={(e) => setOffStoreys(toggle(offStoreys, [''], e.target.checked))} />
                <span>On no storey</span>
              </label>
              <span className="grow" /><span className="num muted">{survey.noStorey}</span>
            </li>
          )}
        </ul>
        {storeysAfter}
      </section>
    </>
  );
}
